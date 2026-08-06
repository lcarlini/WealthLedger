using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly IRepository<FinancialInstitution> _institutionRepository;
    private readonly IRepository<Investment> _investmentRepository;
    private readonly IRepository<CashFlowScheduleItem> _scheduleRepository;
    private readonly ITaskService _taskService;
    private readonly IMarketDataService _marketDataService;

    public DashboardService(
        IRepository<FinancialInstitution> institutionRepository,
        IRepository<Investment> investmentRepository,
        IRepository<CashFlowScheduleItem> scheduleRepository,
        ITaskService taskService,
        IMarketDataService marketDataService)
    {
        _institutionRepository = institutionRepository;
        _investmentRepository = investmentRepository;
        _scheduleRepository = scheduleRepository;
        _taskService = taskService;
        _marketDataService = marketDataService;
    }

    public async Task<DashboardResponse> GetDashboardAsync(int projectionYears = 3)
    {
        projectionYears = Math.Clamp(projectionYears, 1, 100);
        var projectionMonths = projectionYears * 12;
        var institutions = (await _institutionRepository.GetAllAsync()).ToList();
        var allInvestments = (await _investmentRepository.GetAllAsync()).ToList();
        foreach (var inv in allInvestments)
            InvestmentService.NormalizeLifecycleFields(inv);

        var investments = allInvestments.Where(i => i.Status == InvestmentStatus.Active).ToList();
        var archivedCount = allInvestments.Count(i => i.Status == InvestmentStatus.Archived);
        var pendingCount = await _taskService.GetPendingCountAsync();

        var marketData = await _marketDataService.GetMarketDataAsync();
        decimal usdBrl = marketData.UsdBrl > 0 ? marketData.UsdBrl : 0;
        decimal eurBrl = marketData.EurBrl > 0 ? marketData.EurBrl : 0;
        var selic = marketData.SelicPercentPerYear;
        var ipca = marketData.IpcaPercentPerYear;

        decimal ToBrl(Investment i)
        {
            return i.Currency switch
            {
                Currency.USD => i.Amount * usdBrl,
                Currency.EUR => i.Amount * eurBrl,
                _ => i.Amount
            };
        }

        decimal PrincipalBrl(Investment i)
        {
            var p = i.PrincipalAmount > 0 ? i.PrincipalAmount : i.Amount;
            return i.Currency switch
            {
                Currency.USD => p * usdBrl,
                Currency.EUR => p * eurBrl,
                _ => p
            };
        }

        var byType = investments
            .GroupBy(i => i.AccountType)
            .Select(g => new InvestmentsByTypeItem
            {
                AccountType = g.Key.ToString(),
                Count = g.Count(),
                TotalAmount = g.Sum(ToBrl)
            })
            .ToList();

        var scheduleItems = (await _scheduleRepository.GetAllAsync()).ToList();
        var debitTypes = new HashSet<CashFlowItemType>
        {
            CashFlowItemType.Debt,
            CashFlowItemType.Expense,
            CashFlowItemType.CardInstallment
        };

        var now = DateTime.UtcNow;
        var nextMonthDate = new DateTime(now.Year, now.Month, 1).AddMonths(1);
        var nextMonthYear = nextMonthDate.Year;
        var nextMonth = nextMonthDate.Month;

        decimal plannedDebitsProjection = 0;
        for (var i = 0; i < projectionMonths; i++)
        {
            var date = nextMonthDate.AddMonths(i);
            plannedDebitsProjection += scheduleItems
                .Where(item => debitTypes.Contains(item.ItemType) && IsItemActiveInMonth(item, date.Year, date.Month))
                .Sum(item => Math.Abs(item.AmountPerMonth));
        }

        var debitsNextMonth = scheduleItems
            .Where(item => debitTypes.Contains(item.ItemType) && IsItemActiveInMonth(item, nextMonthYear, nextMonth))
            .Sum(item => Math.Abs(item.AmountPerMonth));

        var futureCardDebits = scheduleItems
            .Where(item => item.ItemType == CashFlowItemType.CardInstallment)
            .Sum(item => SumFutureItemAmountFrom(item, nextMonthYear, nextMonth));

        var totalAmount = investments.Sum(ToBrl);
        var totalPrincipal = investments.Sum(PrincipalBrl);
        var unrealized = totalAmount - totalPrincipal;

        decimal weightedReturn = 0;
        decimal weightSum = 0;
        foreach (var inv in investments)
        {
            var w = ToBrl(inv);
            if (w <= 0) continue;
            decimal annual;
            if (AccountTypeRules.IsVariableIncome(inv.AccountType))
                annual = 0;
            else if (inv.Currency == Currency.BRL)
                annual = (selic ?? 0) * (inv.CdiPercentage / 100m);
            else
                annual = inv.AnnualRatePercent ?? 0;

            weightedReturn += annual * w;
            weightSum += w;
        }

        decimal? estimatedAnnual = weightSum > 0 ? Math.Round(weightedReturn / weightSum, 2) : null;
        decimal? excessCdi = estimatedAnnual.HasValue && selic.HasValue
            ? Math.Round(estimatedAnnual.Value - selic.Value, 2)
            : null;
        decimal? excessIpca = estimatedAnnual.HasValue && ipca.HasValue
            ? Math.Round(estimatedAnnual.Value - ipca.Value, 2)
            : null;

        static decimal Project(decimal amount, decimal? annualPct, int years)
        {
            if (amount <= 0 || annualPct is null or <= 0) return amount;
            return Math.Round(amount * (decimal)Math.Pow((double)(1 + annualPct.Value / 100m), years), 2);
        }

        var verdict = BuildPortfolioVerdict(estimatedAnnual, selic, ipca, excessCdi, excessIpca);

        var topHoldings = investments
            .Select(i =>
            {
                var amt = ToBrl(i);
                var prin = PrincipalBrl(i);
                var gainPct = prin > 0 ? Math.Round((amt - prin) / prin * 100m, 2) : 0m;
                var rateLabel = AccountTypeRules.IsVariableIncome(i.AccountType)
                    ? "Mercado"
                    : i.Currency == Currency.BRL
                        ? $"{i.CdiPercentage:0.##}% CDI"
                        : $"{i.AnnualRatePercent:0.##}% a.a.";
                return new DashboardHoldingHighlight
                {
                    InvestmentId = i.Id,
                    Name = i.Name,
                    AmountBrl = amt,
                    GainPercent = gainPct,
                    RateLabel = rateLabel
                };
            })
            .OrderByDescending(h => Math.Abs(h.GainPercent))
            .Take(5)
            .ToList();

        return new DashboardResponse
        {
            InstitutionCount = institutions.Count,
            InvestmentCount = investments.Count,
            ArchivedInvestmentCount = archivedCount,
            TotalAmount = totalAmount,
            TotalPrincipal = totalPrincipal,
            UnrealizedGain = unrealized,
            PlannedDebitsProjection = plannedDebitsProjection,
            ProjectionYears = projectionYears,
            DebitsNextMonth = debitsNextMonth,
            FutureCardDebits = futureCardDebits,
            InvestmentsByType = byType,
            PendingTaskCount = pendingCount,
            EstimatedAnnualReturnPercent = estimatedAnnual,
            SelicPercent = selic,
            IpcaPercent = ipca,
            ExcessOverCdiPercent = excessCdi,
            ExcessOverIpcaPercent = excessIpca,
            ProjectedValue1Y = Project(totalAmount, estimatedAnnual, 1),
            ProjectedValue3Y = Project(totalAmount, estimatedAnnual, 3),
            ProjectedValue5Y = Project(totalAmount, estimatedAnnual, 5),
            PortfolioVerdict = verdict,
            TopHoldings = topHoldings
        };
    }

    private static string BuildPortfolioVerdict(
        decimal? estimated,
        decimal? selic,
        decimal? ipca,
        decimal? excessCdi,
        decimal? excessIpca)
    {
        if (estimated is null)
            return "Sem taxa estimada para a carteira ativa.";
        if (excessIpca is < 0)
            return $"Carteira projetada abaixo da inflação ({ipca:F2}% IPCA). Revisar alocação.";
        if (excessCdi is >= 0.5m)
            return $"Carteira projetada acima do CDI (+{excessCdi:F2} pp vs Selic {selic:F2}%).";
        if (excessCdi is <= -0.5m)
            return $"Carteira projetada abaixo do CDI ({excessCdi:F2} pp). Ainda acima da inflação.";
        return $"Carteira em linha com o CDI (~{selic:F2}% a.a.).";
    }

    private static decimal SumFutureItemAmountFrom(CashFlowScheduleItem item, int fromYear, int fromMonth)
    {
        decimal total = 0;
        var year = item.StartYear;
        var month = item.StartMonth;
        var remainingMonths = item.NumberOfMonths;
        while (remainingMonths > 0)
        {
            if (year > fromYear || (year == fromYear && month >= fromMonth))
                total += Math.Abs(item.AmountPerMonth);

            month++;
            if (month > 12)
            {
                month = 1;
                year++;
            }
            remainingMonths--;
        }
        return total;
    }

    private static bool IsItemActiveInMonth(CashFlowScheduleItem item, int year, int month)
    {
        var currentYear = item.StartYear;
        var currentMonth = item.StartMonth;
        var remainingMonths = item.NumberOfMonths;
        while (remainingMonths > 0)
        {
            if (currentYear == year && currentMonth == month)
                return true;

            currentMonth++;
            if (currentMonth > 12)
            {
                currentMonth = 1;
                currentYear++;
            }
            remainingMonths--;
        }
        return false;
    }
}
