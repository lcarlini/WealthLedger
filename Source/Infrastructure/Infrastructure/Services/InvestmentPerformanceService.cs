using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Requests;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace WealthLedger.Infrastructure.Services;

public class InvestmentPerformanceService : IInvestmentPerformanceService
{
    private readonly IRepository<Investment> _repository;
    private readonly IHistoricalIndexService _historicalIndex;
    private readonly IMarketDataService _marketData;
    private readonly ILogger<InvestmentPerformanceService> _logger;

    public InvestmentPerformanceService(
        IRepository<Investment> repository,
        IHistoricalIndexService historicalIndex,
        IMarketDataService marketData,
        ILogger<InvestmentPerformanceService> logger)
    {
        _repository = repository;
        _historicalIndex = historicalIndex;
        _marketData = marketData;
        _logger = logger;
    }

    public async Task<Investment?> ArchiveAsync(Guid id, ArchiveInvestmentRequest request)
    {
        var inv = await _repository.GetAsync(id);
        if (inv == null) return null;

        var archivedAt = (request.ArchivedAt ?? DateTime.UtcNow).ToUniversalTime();
        var bookAmount = inv.Amount;
        var redeemed = request.RedeemedAmount ?? inv.Amount;

        var start = inv.StartDate == default ? inv.CreatedDate.Date : inv.StartDate.Date;
        var asOf = archivedAt.Date;
        if (asOf < start) asOf = start;
        var principal = inv.PrincipalAmount > 0
            ? inv.PrincipalAmount
            : ResolveVariableCostBasis(inv) ?? inv.Amount;

        decimal? theoretical = null;
        try
        {
            theoretical = await ComputeTheoreticalAmountAsync(inv, principal, start, asOf, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not compute theoretical amount at archive for {Id}", id);
        }

        inv.Status = InvestmentStatus.Archived;
        inv.ArchivedAt = archivedAt;
        inv.BookAmountAtArchive = Round2(bookAmount);
        inv.ExpectedTheoreticalAtArchive = theoretical;
        inv.RedeemedAmount = Round2(redeemed);
        inv.ArchiveNotes = string.IsNullOrWhiteSpace(request.Notes) ? inv.ArchiveNotes : request.Notes.Trim();
        inv.Amount = inv.RedeemedAmount.Value;
        inv.UpdatedDate = DateTime.UtcNow;
        return await _repository.UpsertAsync(inv);
    }

    public async Task<Investment?> RestoreAsync(Guid id)
    {
        var inv = await _repository.GetAsync(id);
        if (inv == null) return null;

        inv.Status = InvestmentStatus.Active;
        inv.ArchivedAt = null;
        inv.RedeemedAmount = null;
        inv.ArchiveNotes = null;
        inv.BookAmountAtArchive = null;
        inv.ExpectedTheoreticalAtArchive = null;
        inv.UpdatedDate = DateTime.UtcNow;
        return await _repository.UpsertAsync(inv);
    }

    public async Task<InvestmentPerformanceResponse?> GetPerformanceAsync(Guid investmentId, CancellationToken ct = default)
    {
        var inv = await _repository.GetAsync(investmentId);
        if (inv == null) return null;

        var start = inv.StartDate == default ? inv.CreatedDate.Date : inv.StartDate.Date;
        var asOf = inv.Status == InvestmentStatus.Archived && inv.ArchivedAt.HasValue
            ? inv.ArchivedAt.Value.Date
            : DateTime.UtcNow.Date;
        if (asOf < start) asOf = start;

        var holdingDays = Math.Max(0, (asOf - start).Days);
        var principal = inv.PrincipalAmount > 0
            ? inv.PrincipalAmount
            : ResolveVariableCostBasis(inv) ?? inv.Amount;
        var current = inv.Status == InvestmentStatus.Archived && inv.RedeemedAmount.HasValue
            ? inv.RedeemedAmount.Value
            : inv.Amount;

        var response = new InvestmentPerformanceResponse
        {
            InvestmentId = inv.Id,
            Name = inv.Name,
            AccountType = inv.AccountType,
            Currency = inv.Currency,
            Status = inv.Status,
            StartDate = start,
            AsOfDate = asOf,
            HoldingDays = holdingDays,
            PrincipalAmount = principal,
            CurrentAmount = current,
            IsIncomeTaxExempt = inv.IsIncomeTaxExempt || inv.AccountType == AccountType.SavingsBox
        };

        if (AccountTypeRules.IsVariableIncome(inv.AccountType))
            await FillVariableIncomeAsync(response, inv, ct);
        else
            await FillFixedIncomeAsync(response, inv, ct);

        FillRedemptionAnalysis(response, inv);

        response.Projections = inv.Status == InvestmentStatus.Archived
            ? []
            : await BuildProjectionsAsync(inv, response, ct);
        return response;
    }

    private async Task<decimal?> ComputeTheoreticalAmountAsync(
        Investment inv,
        decimal principal,
        DateTime start,
        DateTime asOf,
        CancellationToken ct)
    {
        if (AccountTypeRules.IsVariableIncome(inv.AccountType))
            return null;

        if (inv.Currency == Currency.BRL && inv.CdiPercentage >= 0)
        {
            var window = await _historicalIndex.GetWindowAsync(start, asOf, ct);
            var pct = inv.CdiPercentage / 100m;
            var factor = pct == 0
                ? 1m
                : (decimal)Math.Pow((double)window.CdiAccumulatedFactor, (double)pct);
            return Round2(principal * factor);
        }

        var rate = inv.AnnualRatePercent ?? 0m;
        var holdingDays = Math.Max(0, (asOf - start).Days);
        var years = holdingDays / 365.25m;
        var annualFactor = (decimal)Math.Pow((double)(1 + rate / 100m), (double)years);
        return Round2(principal * annualFactor);
    }

    private static void FillRedemptionAnalysis(InvestmentPerformanceResponse r, Investment inv)
    {
        if (inv.Status != InvestmentStatus.Archived || !inv.RedeemedAmount.HasValue)
            return;

        var redeemed = inv.RedeemedAmount.Value;
        var book = inv.BookAmountAtArchive;
        var theo = inv.ExpectedTheoreticalAtArchive ?? r.AccruedTheoreticalAmount;

        r.BookAmount = book;
        r.TheoreticalAmount = theo;

        if (book.HasValue)
        {
            r.VsBookDelta = Round2(redeemed - book.Value);
            r.VsBookPercent = book.Value != 0
                ? Round2(r.VsBookDelta.Value / book.Value * 100m)
                : 0m;
        }

        if (theo.HasValue)
        {
            r.VsTheoreticalDelta = Round2(redeemed - theo.Value);
            r.VsTheoreticalPercent = theo.Value != 0
                ? Round2(r.VsTheoreticalDelta.Value / theo.Value * 100m)
                : 0m;
        }

        var (verdict, summary) = ClassifyRedemption(redeemed, book, theo);
        r.RedemptionVerdict = verdict;
        r.RedemptionSummary = summary;
    }

    private static (string Verdict, string Summary) ClassifyRedemption(
        decimal redeemed,
        decimal? book,
        decimal? theo)
    {
        static int Sign(decimal redeemed, decimal baseline)
        {
            var tol = Math.Max(0.50m, Math.Abs(baseline) * 0.001m);
            var delta = redeemed - baseline;
            if (delta > tol) return 1;
            if (delta < -tol) return -1;
            return 0;
        }

        var vsBook = book.HasValue ? Sign(redeemed, book.Value) : 0;
        var vsTheo = theo.HasValue ? Sign(redeemed, theo.Value) : 0;
        var hasBook = book.HasValue;
        var hasTheo = theo.HasValue;

        if ((!hasBook || vsBook == 0) && (!hasTheo || vsTheo == 0))
        {
            return ("match", "Resgate em linha com carteira e teórico.");
        }

        if (hasBook && hasTheo && vsBook == 1 && vsTheo == 1)
            return ("above_both", "Resgate acima da carteira e do teórico.");
        if (hasBook && hasTheo && vsBook == -1 && vsTheo == -1)
            return ("below_both", "Resgate abaixo da carteira e do teórico.");
        if (hasBook && hasTheo && vsBook != 0 && vsTheo != 0 && vsBook != vsTheo)
            return ("mixed", "Resgate acima de um baseline e abaixo do outro.");

        if (vsBook == 1)
            return ("above_book", "Resgate acima do saldo em carteira.");
        if (vsTheo == 1)
            return ("above_theoretical", "Resgate acima do valor teórico (índice/taxa).");
        if (vsBook == -1)
            return ("below_book", "Resgate abaixo do saldo em carteira.");
        if (vsTheo == -1)
            return ("below_theoretical", "Resgate abaixo do valor teórico (índice/taxa).");

        return ("match", "Resgate em linha com os baselines disponíveis.");
    }

    private async Task FillFixedIncomeAsync(InvestmentPerformanceResponse r, Investment inv, CancellationToken ct)
    {
        var window = await _historicalIndex.GetWindowAsync(r.StartDate, r.AsOfDate, ct);
        r.CdiAccumulatedPercent = Round2(window.CdiAccumulatedPercent);
        r.IpcaAccumulatedPercent = Round2(window.IpcaAccumulatedPercent);

        decimal theoretical;
        if (inv.Currency == Currency.BRL && inv.CdiPercentage >= 0)
        {
            // Accrue principal at (CDI factor)^(cdi%/100) ≈ product of daily^(pct/100)
            // Exact: factor = cdiFactor ^ (cdiPercentage/100)
            var pct = inv.CdiPercentage / 100m;
            var factor = pct == 0
                ? 1m
                : (decimal)Math.Pow((double)window.CdiAccumulatedFactor, (double)pct);
            theoretical = Round2(r.PrincipalAmount * factor);
            r.AccruedTheoreticalAmount = theoretical;
            r.CdiEquivalentAccumulatedPercent = Round2((factor - 1m) * 100m);
        }
        else
        {
            // Foreign / fixed annual rate: compound by calendar years
            var rate = inv.AnnualRatePercent ?? 0m;
            var years = r.HoldingDays / 365.25m;
            var factor = (decimal)Math.Pow((double)(1 + rate / 100m), (double)years);
            theoretical = Round2(r.PrincipalAmount * factor);
            r.AccruedTheoreticalAmount = theoretical;
            r.CdiEquivalentAccumulatedPercent = Round2((factor - 1m) * 100m);
        }

        // Prefer book Amount when it diverges (manual mark); else theoretical
        var evaluationAmount = r.CurrentAmount;
        // If user never updated Amount and it still equals principal, use theoretical accrual
        if (Math.Abs(r.CurrentAmount - r.PrincipalAmount) < 0.01m && theoretical > r.PrincipalAmount)
            evaluationAmount = theoretical;

        r.GrossGain = Round2(evaluationAmount - r.PrincipalAmount);
        r.GrossGainPercent = r.PrincipalAmount > 0
            ? Round2(r.GrossGain / r.PrincipalAmount * 100m)
            : 0m;

        var applyIof = inv.AccountType != AccountType.SavingsBox && inv.Currency == Currency.BRL;
        var (iof, iofRate, ir, irRate, netGain) = BrazilianInvestmentTax.ApplyTaxes(
            r.GrossGain, r.HoldingDays, r.IsIncomeTaxExempt, applyIof);

        r.IofAmount = iof;
        r.IofRatePercent = iofRate;
        r.IrAmount = ir;
        r.IrRatePercent = irRate;
        r.NetGain = Round2(netGain);
        r.NetAmount = Round2(r.PrincipalAmount + r.NetGain);
        r.NetGainPercent = r.PrincipalAmount > 0
            ? Round2(r.NetGain / r.PrincipalAmount * 100m)
            : 0m;

        FillInflationAndVerdict(r, window.Notes);
        FillBreakdown(r, theoretical);
    }

    private async Task FillVariableIncomeAsync(InvestmentPerformanceResponse r, Investment inv, CancellationToken ct)
    {
        r.GrossGain = Round2(r.CurrentAmount - r.PrincipalAmount);
        r.GrossGainPercent = r.PrincipalAmount > 0
            ? Round2(r.GrossGain / r.PrincipalAmount * 100m)
            : 0m;

        var cgRate = inv.AccountType == AccountType.FII ? 20m : 15m;
        r.IrRatePercent = r.GrossGain > 0 ? cgRate : 0m;
        r.IrAmount = r.GrossGain > 0
            ? Round2(r.GrossGain * cgRate / 100m)
            : 0m;
        r.IofAmount = 0;
        r.IofRatePercent = 0;
        r.NetGain = Round2(r.GrossGain - r.IrAmount);
        r.NetAmount = Round2(r.PrincipalAmount + r.NetGain);
        r.NetGainPercent = r.PrincipalAmount > 0
            ? Round2(r.NetGain / r.PrincipalAmount * 100m)
            : 0m;

        try
        {
            var window = await _historicalIndex.GetWindowAsync(r.StartDate, r.AsOfDate, ct);
            r.CdiAccumulatedPercent = Round2(window.CdiAccumulatedPercent);
            r.IpcaAccumulatedPercent = Round2(window.IpcaAccumulatedPercent);
            FillInflationAndVerdict(r, window.Notes);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load benchmarks for variable income {Id}", inv.Id);
            FillInflationAndVerdict(r, null);
        }

        FillBreakdown(r, null);
    }

    private static void FillInflationAndVerdict(InvestmentPerformanceResponse r, string? notes)
    {
        if (r.IpcaAccumulatedPercent is > 0 && r.PrincipalAmount > 0)
        {
            var realFactor = (1 + r.NetGainPercent / 100m) / (1 + r.IpcaAccumulatedPercent.Value / 100m);
            r.RealGainPercent = Round2((realFactor - 1m) * 100m);
            r.ExcessOverIpcaPercent = Round2(r.NetGainPercent - r.IpcaAccumulatedPercent.Value);
        }

        if (r.CdiAccumulatedPercent.HasValue)
            r.ExcessOverCdiPercent = Round2(r.NetGainPercent - r.CdiAccumulatedPercent.Value);

        var vsCdi = r.ExcessOverCdiPercent;
        var vsIpca = r.ExcessOverIpcaPercent;

        if (vsIpca is < 0)
        {
            r.Verdict = "lag_ipca";
            r.VerdictSummary = $"Abaixo da inflação ({r.IpcaAccumulatedPercent:F2}% IPCA no período). Retorno real negativo.";
        }
        else if (vsCdi is >= 0.25m)
        {
            r.Verdict = "beat_cdi";
            r.VerdictSummary = $"Acima do CDI no período (+{vsCdi:F2} pp). Bom resultado líquido.";
        }
        else if (vsCdi is <= -0.25m)
        {
            r.Verdict = "lag_cdi";
            r.VerdictSummary = $"Abaixo do CDI no período ({vsCdi:F2} pp). Ainda pode bater a inflação.";
        }
        else
        {
            r.Verdict = "match_cdi";
            r.VerdictSummary = "Em linha com o CDI no período.";
        }

        if (!string.IsNullOrWhiteSpace(notes) && notes.Contains("Fallback", StringComparison.OrdinalIgnoreCase))
            r.VerdictSummary += " (índices estimados — série BCB indisponível.)";
    }

    private static void FillBreakdown(InvestmentPerformanceResponse r, decimal? theoretical)
    {
        r.Breakdown =
        [
            new PerformanceBreakdownItem { Key = "principal", Label = "Valor aplicado", Amount = r.PrincipalAmount },
            new PerformanceBreakdownItem { Key = "gross", Label = "Rendimento bruto", Amount = r.GrossGain },
            new PerformanceBreakdownItem
            {
                Key = "iof",
                Label = $"IOF ({r.IofRatePercent:F0}%)",
                Amount = -r.IofAmount,
                Note = r.HoldingDays < 30 ? "Resgate antes de 30 dias" : "Isento (≥ 30 dias)"
            },
            new PerformanceBreakdownItem
            {
                Key = "ir",
                Label = r.IsIncomeTaxExempt ? "IR (isento)" : $"IR ({r.IrRatePercent:F1}%)",
                Amount = -r.IrAmount,
                Note = r.IsIncomeTaxExempt ? "Produto isento de IR" : "Tabela regressiva"
            },
            new PerformanceBreakdownItem { Key = "net", Label = "Valor líquido estimado", Amount = r.NetAmount },
        ];

        if (theoretical.HasValue)
        {
            r.Breakdown.Insert(1, new PerformanceBreakdownItem
            {
                Key = "theoretical",
                Label = "Valor teórico (índice)",
                Amount = theoretical.Value,
                Note = "Principal × CDI do período × % contratado"
            });
        }
    }

    private async Task<List<PerformanceProjectionPoint>> BuildProjectionsAsync(
        Investment inv,
        InvestmentPerformanceResponse current,
        CancellationToken ct)
    {
        var market = await _marketData.GetMarketDataAsync();
        decimal annualRate;
        if (AccountTypeRules.IsVariableIncome(inv.AccountType))
            annualRate = 0; // no deterministic projection for VI
        else if (inv.Currency == Currency.BRL)
            annualRate = (market.SelicPercentPerYear ?? 0) * (inv.CdiPercentage / 100m);
        else
            annualRate = inv.AnnualRatePercent ?? 0;

        if (annualRate <= 0)
            return [];

        // Project from the GROSS value: tax is applied once on (gross - principal) below.
        // Starting from NetAmount would tax the gain already earned twice.
        var evaluated = current.PrincipalAmount + current.GrossGain;
        var baseAmount = evaluated > 0 ? evaluated : current.CurrentAmount;
        var points = new List<PerformanceProjectionPoint>();
        foreach (var months in new[] { 3, 6, 12, 24, 36 })
        {
            var years = months / 12m;
            var gross = Round2(baseAmount * (decimal)Math.Pow((double)(1 + annualRate / 100m), (double)years));
            // IR uses the total holding time from the start date at the projected date.
            var fullHolding = current.HoldingDays + (int)Math.Round(months * 30.4375);
            var (_, _, _, _, netFromPrincipal) = BrazilianInvestmentTax.ApplyTaxes(
                gross - current.PrincipalAmount,
                fullHolding,
                current.IsIncomeTaxExempt,
                inv.AccountType != AccountType.SavingsBox && inv.Currency == Currency.BRL);
            var net = Round2(current.PrincipalAmount + netFromPrincipal);
            points.Add(new PerformanceProjectionPoint
            {
                MonthsAhead = months,
                Label = months switch
                {
                    3 => "3 meses",
                    6 => "6 meses",
                    12 => "1 ano",
                    24 => "2 anos",
                    36 => "3 anos",
                    _ => $"{months}m"
                },
                ProjectedGross = gross,
                ProjectedNet = net,
                ProjectedNetGainPercent = current.PrincipalAmount > 0
                    ? Round2((net - current.PrincipalAmount) / current.PrincipalAmount * 100m)
                    : 0
            });
        }

        return points;
    }

    private static decimal? ResolveVariableCostBasis(Investment inv)
    {
        if (inv.Quantity is > 0 && inv.AveragePrice is > 0)
            return Math.Round(inv.Quantity.Value * inv.AveragePrice.Value, 2, MidpointRounding.AwayFromZero);
        return null;
    }

    private static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
