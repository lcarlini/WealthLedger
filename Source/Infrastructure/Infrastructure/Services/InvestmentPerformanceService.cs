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

        inv.Status = InvestmentStatus.Archived;
        inv.ArchivedAt = (request.ArchivedAt ?? DateTime.UtcNow).ToUniversalTime();
        inv.RedeemedAmount = request.RedeemedAmount ?? inv.Amount;
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

        response.Projections = await BuildProjectionsAsync(inv, response, ct);
        return response;
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

        var baseAmount = current.NetAmount > 0 ? current.NetAmount : current.CurrentAmount;
        var points = new List<PerformanceProjectionPoint>();
        foreach (var months in new[] { 3, 6, 12, 24, 36 })
        {
            var years = months / 12m;
            var gross = Round2(baseAmount * (decimal)Math.Pow((double)(1 + annualRate / 100m), (double)years));
            var holdingDays = current.HoldingDays + (int)Math.Round(months * 30.4375);
            var gain = gross - baseAmount;
            var applyIof = inv.AccountType != AccountType.SavingsBox && inv.Currency == Currency.BRL && months < 1;
            var (_, _, _, _, netGain) = BrazilianInvestmentTax.ApplyTaxes(
                gain, holdingDays, current.IsIncomeTaxExempt, applyIof);
            // For forward projections beyond 30 days IOF=0; IR uses future holding days from start
            var fullHolding = current.HoldingDays + (int)Math.Round(months * 30.4375);
            var (iof, _, ir, _, netFromPrincipal) = BrazilianInvestmentTax.ApplyTaxes(
                gross - current.PrincipalAmount,
                fullHolding,
                current.IsIncomeTaxExempt,
                inv.AccountType != AccountType.SavingsBox && inv.Currency == Currency.BRL);
            _ = (iof, netGain);
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
