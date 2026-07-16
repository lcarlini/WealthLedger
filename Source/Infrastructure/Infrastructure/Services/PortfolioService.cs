using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace WealthLedger.Infrastructure.Services;

public class PortfolioService : IPortfolioService
{
    private readonly IRepository<Investment> _investmentRepository;
    private readonly IRepository<FinancialInstitution> _institutionRepository;
    private readonly IRepository<PassiveIncome> _passiveIncomeRepository;
    private readonly IRepository<InvestmentGoal> _goalRepository;
    private readonly IRepository<PortfolioSnapshot> _snapshotRepository;
    private readonly IRepository<CashFlowScheduleItem> _scheduleRepository;
    private readonly IMarketDataService _marketDataService;
    private readonly ITaskService _taskService;
    private readonly ILogger<PortfolioService> _logger;

    public PortfolioService(
        IRepository<Investment> investmentRepository,
        IRepository<FinancialInstitution> institutionRepository,
        IRepository<PassiveIncome> passiveIncomeRepository,
        IRepository<InvestmentGoal> goalRepository,
        IRepository<PortfolioSnapshot> snapshotRepository,
        IRepository<CashFlowScheduleItem> scheduleRepository,
        IMarketDataService marketDataService,
        ITaskService taskService,
        ILogger<PortfolioService> logger)
    {
        _investmentRepository = investmentRepository;
        _institutionRepository = institutionRepository;
        _passiveIncomeRepository = passiveIncomeRepository;
        _goalRepository = goalRepository;
        _snapshotRepository = snapshotRepository;
        _scheduleRepository = scheduleRepository;
        _marketDataService = marketDataService;
        _taskService = taskService;
        _logger = logger;
    }

    public async Task<PortfolioOverviewResponse> GetOverviewAsync(
        int projectionYears = 3,
        CancellationToken cancellationToken = default)
    {
        projectionYears = Math.Clamp(projectionYears, 1, 100);
        var investments = (await _investmentRepository.GetAllAsync()).ToList();
        var institutions = (await _institutionRepository.GetAllAsync()).ToList();
        var passive = (await _passiveIncomeRepository.GetAllAsync()).ToList();
        var goals = (await _goalRepository.GetAllAsync()).ToList();
        var snapshots = (await _snapshotRepository.GetAllAsync())
            .OrderBy(s => s.SnapshotDate)
            .ToList();
        var schedule = (await _scheduleRepository.GetAllAsync()).ToList();

        MarketDataResponse market;
        try
        {
            market = await _marketDataService.GetMarketDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Market data unavailable for portfolio overview");
            market = new MarketDataResponse();
        }

        decimal ToBrl(decimal amount, Currency currency) => currency switch
        {
            Currency.USD => amount * (market.UsdBrl > 0 ? market.UsdBrl : 0),
            Currency.EUR => amount * (market.EurBrl > 0 ? market.EurBrl : 0),
            _ => amount
        };

        var holdings = new List<HoldingPerformanceItem>();
        decimal totalBrl = 0, cashBrl = 0, fixedBrl = 0, variableBrl = 0;
        decimal totalCostBrl = 0, unrealizedBrl = 0;

        foreach (var inv in investments)
        {
            var inst = institutions.FirstOrDefault(i => i.Id == inv.FinancialInstitutionId);
            var marketValueBrl = ToBrl(inv.Amount, inv.Currency);
            totalBrl += marketValueBrl;

            var isVariable = AccountTypeRules.IsVariableIncome(inv.AccountType);
            if (isVariable) variableBrl += marketValueBrl;
            else if (inv.AccountType == AccountType.FixedTerm) fixedBrl += marketValueBrl;
            else cashBrl += marketValueBrl;

            decimal? costBasis = null;
            decimal? costBasisBrl = null;
            decimal? unrealized = null;
            decimal? unrealizedLocalBrl = null;
            decimal? unrealizedPct = null;

            if (isVariable && inv.Quantity is > 0 && inv.AveragePrice is > 0)
            {
                costBasis = inv.Quantity.Value * inv.AveragePrice.Value;
                costBasisBrl = ToBrl(costBasis.Value, inv.Currency);
                unrealized = inv.Amount - costBasis.Value;
                unrealizedLocalBrl = marketValueBrl - costBasisBrl.Value;
                unrealizedPct = costBasis.Value > 0 ? unrealized.Value / costBasis.Value * 100 : null;
                totalCostBrl += costBasisBrl.Value;
                unrealizedBrl += unrealizedLocalBrl.Value;
            }
            else
            {
                totalCostBrl += marketValueBrl;
            }

            holdings.Add(new HoldingPerformanceItem
            {
                InvestmentId = inv.Id,
                Name = inv.Name,
                InstitutionName = inst?.Name,
                AccountType = inv.AccountType.ToString(),
                Currency = inv.Currency.ToString(),
                Ticker = inv.Ticker,
                Quantity = inv.Quantity,
                AveragePrice = inv.AveragePrice,
                MarketValue = inv.Amount,
                MarketValueBrl = marketValueBrl,
                CostBasis = costBasis,
                CostBasisBrl = costBasisBrl,
                UnrealizedGain = unrealized,
                UnrealizedGainBrl = unrealizedLocalBrl,
                UnrealizedGainPercent = unrealizedPct,
                IsVariableIncome = isVariable
            });
        }

        var yearStart = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        decimal PassiveToBrl(PassiveIncome p) => ToBrl(p.Amount, p.Currency);
        var passiveTotal = passive.Sum(PassiveToBrl);
        var passiveYtd = passive.Where(p => p.PaymentDate >= yearStart).Sum(PassiveToBrl);

        var allocationByType = BuildAllocation(
            investments.GroupBy(i => i.AccountType.ToString()),
            i => ToBrl(i.Amount, i.Currency),
            totalBrl);

        var allocationByCurrency = BuildAllocation(
            investments.GroupBy(i => i.Currency.ToString()),
            i => ToBrl(i.Amount, i.Currency),
            totalBrl);

        var allocationByInstitution = BuildAllocation(
            investments.GroupBy(i => institutions.FirstOrDefault(x => x.Id == i.FinancialInstitutionId)?.Name ?? "Unknown"),
            i => ToBrl(i.Amount, i.Currency),
            totalBrl);

        var estimatedAnnual = EstimatePortfolioAnnualReturn(investments, market);
        var benchmarks = BuildBenchmarks(estimatedAnnual, market);
        var rebalance = BuildRebalanceSuggestions(cashBrl, fixedBrl, variableBrl, totalBrl);
        var calendar = await BuildCalendarAsync(investments, institutions, passive, projectionYears * 12);
        var health = BuildHealthScore(totalBrl, cashBrl, fixedBrl, variableBrl, allocationByType, goals, schedule, market);
        var portfolioScore = BuildPortfolioScore(holdings, allocationByType, variableBrl, totalBrl, estimatedAnnual, market);
        var insights = BuildInsights(health, portfolioScore, benchmarks, rebalance, passiveYtd, unrealizedBrl);

        return new PortfolioOverviewResponse
        {
            TotalNetWorthBrl = totalBrl,
            TotalCostBasisBrl = totalCostBrl,
            UnrealizedGainBrl = unrealizedBrl,
            UnrealizedGainPercent = totalCostBrl > 0 ? unrealizedBrl / totalCostBrl * 100 : 0,
            RealizedPassiveIncomeBrl = passiveTotal,
            RealizedPassiveIncomeYtdBrl = passiveYtd,
            CashAmountBrl = cashBrl,
            FixedIncomeAmountBrl = fixedBrl,
            VariableIncomeAmountBrl = variableBrl,
            InvestmentCount = investments.Count,
            InstitutionCount = institutions.Count,
            Health = health,
            PortfolioScore = portfolioScore,
            AllocationByType = allocationByType,
            AllocationByCurrency = allocationByCurrency,
            AllocationByInstitution = allocationByInstitution,
            Holdings = holdings.OrderByDescending(h => h.MarketValueBrl).ToList(),
            Benchmarks = benchmarks,
            RebalanceSuggestions = rebalance,
            Calendar = calendar,
            Timeline = snapshots.Select(MapSnapshot).ToList(),
            Insights = insights
        };
    }

    public async Task<PortfolioSnapshot> CaptureSnapshotAsync(string? notes = null, CancellationToken cancellationToken = default)
    {
        var overview = await GetOverviewAsync(cancellationToken: cancellationToken);
        var today = DateTime.UtcNow.Date;

        var existing = (await _snapshotRepository.GetAllAsync())
            .FirstOrDefault(s => s.SnapshotDate.Date == today);

        var snapshot = existing ?? new PortfolioSnapshot { Id = Guid.NewGuid() };
        snapshot.SnapshotDate = today;
        snapshot.TotalAmountBrl = overview.TotalNetWorthBrl;
        snapshot.CashAmountBrl = overview.CashAmountBrl;
        snapshot.FixedIncomeAmountBrl = overview.FixedIncomeAmountBrl;
        snapshot.VariableIncomeAmountBrl = overview.VariableIncomeAmountBrl;
        snapshot.UnrealizedGainBrl = overview.UnrealizedGainBrl;
        snapshot.InvestmentCount = overview.InvestmentCount;
        snapshot.Notes = notes;
        snapshot.UpdatedDate = DateTime.UtcNow;
        if (existing == null)
            snapshot.CreatedDate = DateTime.UtcNow;

        return await _snapshotRepository.UpsertAsync(snapshot);
    }

    public async Task<IEnumerable<PortfolioSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default)
    {
        return (await _snapshotRepository.GetAllAsync()).OrderBy(s => s.SnapshotDate);
    }

    public async Task<IEnumerable<CalendarEventItem>> GetCalendarAsync(int monthsAhead = 12, CancellationToken cancellationToken = default)
    {
        var investments = (await _investmentRepository.GetAllAsync()).ToList();
        var institutions = (await _institutionRepository.GetAllAsync()).ToList();
        var passive = (await _passiveIncomeRepository.GetAllAsync()).ToList();
        return await BuildCalendarAsync(investments, institutions, passive, monthsAhead);
    }

    private static List<AllocationSlice> BuildAllocation(
        IEnumerable<IGrouping<string, Investment>> groups,
        Func<Investment, decimal> toBrl,
        decimal totalBrl)
    {
        return groups
            .Select(g =>
            {
                var amount = g.Sum(toBrl);
                return new AllocationSlice
                {
                    Key = g.Key,
                    Label = g.Key,
                    AmountBrl = amount,
                    Percent = totalBrl > 0 ? amount / totalBrl * 100 : 0,
                    Count = g.Count()
                };
            })
            .OrderByDescending(a => a.AmountBrl)
            .ToList();
    }

    private static decimal EstimatePortfolioAnnualReturn(List<Investment> investments, MarketDataResponse market)
    {
        if (investments.Count == 0) return 0;
        var selic = market.SelicPercentPerYear ?? 10;
        decimal weighted = 0;
        decimal total = 0;
        foreach (var inv in investments)
        {
            var weight = inv.Amount;
            if (weight <= 0) continue;
            total += weight;
            decimal rate;
            if (AccountTypeRules.IsVariableIncome(inv.AccountType))
                rate = 0; // unknown; exclude from deterministic estimate weight by treating as 0 contribution to known yield
            else if (inv.Currency == Currency.BRL)
                rate = selic * (inv.CdiPercentage / 100);
            else
                rate = inv.AnnualRatePercent ?? 0;
            weighted += weight * rate;
        }

        return total > 0 ? weighted / total : 0;
    }

    private static List<BenchmarkComparisonItem> BuildBenchmarks(decimal portfolioEstimated, MarketDataResponse market)
    {
        var selic = market.SelicPercentPerYear ?? 0;
        var ipca = market.IpcaPercentPerYear ?? 0;
        var poupanca = market.PoupancaPercentPerYear ?? 0;

        // Synthetic long-run equity/crypto expectations for comparison (user-adjustable conceptually).
        var items = new List<BenchmarkComparisonItem>
        {
            MakeBenchmark("CDI", "CDI (Selic proxy)", selic, portfolioEstimated),
            MakeBenchmark("IPCA", "IPCA (inflation)", ipca, portfolioEstimated),
            MakeBenchmark("POUPANCA", "Poupança", poupanca, portfolioEstimated),
            MakeBenchmark("IPCA+", "IPCA + 5%", ipca + 5, portfolioEstimated),
            MakeBenchmark("IBOV", "Ibovespa (illustrative 10% a.a.)", 10, portfolioEstimated),
            MakeBenchmark("SP500", "S&P 500 (illustrative 8% a.a.)", 8, portfolioEstimated),
            MakeBenchmark("BTC", "Bitcoin (illustrative — high risk)", 0, portfolioEstimated),
        };

        if (market.BtcUsd is > 0)
        {
            // BTC has no stable annual rate; keep illustrative note via 0 and message in UI.
        }

        return items;
    }

    private static BenchmarkComparisonItem MakeBenchmark(string code, string name, decimal bench, decimal portfolio)
    {
        return new BenchmarkComparisonItem
        {
            Code = code,
            Name = name,
            AnnualRatePercent = bench,
            PortfolioEstimatedAnnualPercent = portfolio,
            DifferencePercent = portfolio - bench,
            PortfolioBeats = portfolio >= bench,
            PeriodLabel = "Estimated a.a."
        };
    }

    private static List<RebalanceSuggestion> BuildRebalanceSuggestions(
        decimal cash, decimal fixedIncome, decimal variable, decimal total)
    {
        if (total <= 0) return [];

        // Simple target mix inspired by balanced BR retail portfolios.
        var targets = new Dictionary<string, decimal>
        {
            ["Cash / liquidity"] = 15,
            ["Fixed income"] = 45,
            ["Variable income"] = 40
        };

        var current = new Dictionary<string, decimal>
        {
            ["Cash / liquidity"] = cash / total * 100,
            ["Fixed income"] = fixedIncome / total * 100,
            ["Variable income"] = variable / total * 100
        };

        var suggestions = new List<RebalanceSuggestion>();
        foreach (var (category, target) in targets)
        {
            var cur = current[category];
            var drift = cur - target;
            if (Math.Abs(drift) < 5) continue;

            suggestions.Add(new RebalanceSuggestion
            {
                Category = category,
                CurrentPercent = Math.Round(cur, 1),
                TargetPercent = target,
                DriftPercent = Math.Round(drift, 1),
                Action = drift > 0 ? "Reduce" : "Increase",
                Message = drift > 0
                    ? $"{category} is {Math.Abs(drift):0.0}pp above the balanced target ({target}%). Consider reallocating."
                    : $"{category} is {Math.Abs(drift):0.0}pp below the balanced target ({target}%). Consider adding exposure."
            });
        }

        return suggestions;
    }

    private async Task<List<CalendarEventItem>> BuildCalendarAsync(
        List<Investment> investments,
        List<FinancialInstitution> institutions,
        List<PassiveIncome> passive,
        int monthsAhead)
    {
        var events = new List<CalendarEventItem>();
        var today = DateTime.UtcNow.Date;
        var end = today.AddMonths(Math.Max(1, monthsAhead));

        foreach (var inv in investments.Where(i => i.MaturityDate.HasValue))
        {
            var mat = inv.MaturityDate!.Value.Date;
            if (mat >= today && mat <= end)
            {
                events.Add(new CalendarEventItem
                {
                    Date = mat,
                    Title = $"Maturity: {inv.Name}",
                    EventType = "Maturity",
                    InvestmentName = inv.Name,
                    Amount = inv.Amount,
                    Currency = inv.Currency.ToString()
                });
            }
        }

        foreach (var inv in investments.Where(i => i.RequiresMonthlyMovement))
        {
            var cursor = new DateTime(today.Year, today.Month, 1).AddMonths(1);
            while (cursor <= end)
            {
                events.Add(new CalendarEventItem
                {
                    Date = cursor,
                    Title = $"Monthly movement: {inv.Name}",
                    EventType = "MonthlyMovement",
                    InvestmentName = inv.Name,
                    Amount = inv.MonthlyMovementAmount,
                    Currency = inv.Currency.ToString()
                });
                cursor = cursor.AddMonths(1);
            }
        }

        foreach (var p in passive.Where(x => x.PaymentDate.Date >= today.AddMonths(-1) && x.PaymentDate.Date <= end))
        {
            events.Add(new CalendarEventItem
            {
                Date = p.PaymentDate.Date,
                Title = $"{p.IncomeType}: {p.Name}",
                EventType = "PassiveIncome",
                InvestmentName = investments.FirstOrDefault(i => i.Id == p.InvestmentId)?.Name,
                Amount = p.Amount,
                Currency = p.Currency.ToString()
            });
        }

        try
        {
            var future = await _taskService.GetFutureTasksAsync(monthsAhead);
            foreach (var t in future)
            {
                var day = t.DueDay ?? 1;
                var date = new DateTime(t.DueYear, t.DueMonth, Math.Min(day, DateTime.DaysInMonth(t.DueYear, t.DueMonth)));
                if (events.Any(e => e.Title == t.Title && e.Date.Year == date.Year && e.Date.Month == date.Month))
                    continue;
                events.Add(new CalendarEventItem
                {
                    Date = date,
                    Title = t.Title,
                    EventType = t.TaskType,
                    InvestmentName = t.InvestmentName,
                    Amount = t.Amount,
                    Currency = t.Currency
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load future tasks for calendar");
        }

        return events.OrderBy(e => e.Date).ThenBy(e => e.Title).ToList();
    }

    private static FinancialHealthScore BuildHealthScore(
        decimal totalBrl,
        decimal cashBrl,
        decimal fixedBrl,
        decimal variableBrl,
        List<AllocationSlice> byType,
        List<InvestmentGoal> goals,
        List<CashFlowScheduleItem> schedule,
        MarketDataResponse market)
    {
        var notes = new List<string>();

        // Liquidity: cash share 10–25% ideal
        var cashPct = totalBrl > 0 ? cashBrl / totalBrl * 100 : 0;
        var liquidity = cashPct switch
        {
            >= 10 and <= 30 => 90,
            >= 5 and < 10 => 70,
            > 30 and <= 50 => 65,
            > 50 => 40,
            _ => 35
        };
        if (cashPct < 5) notes.Add("Low cash buffer — emergency liquidity may be tight.");
        if (cashPct > 40) notes.Add("High cash share — consider deploying idle cash.");

        // Diversification: number of types + HHI-like concentration
        var types = byType.Count;
        var topPct = byType.FirstOrDefault()?.Percent ?? 100;
        var diversification = Math.Clamp(types * 15 + (topPct < 40 ? 30 : topPct < 60 ? 15 : 0), 0, 100);
        if (types < 3) notes.Add("Few asset classes — diversification is limited.");
        if (topPct > 60) notes.Add($"Top category is {topPct:0}% of portfolio — concentration risk.");

        // Inflation hedge: variable + fixed with decent CDI
        var riskAssetsPct = totalBrl > 0 ? (variableBrl + fixedBrl) / totalBrl * 100 : 0;
        var inflationHedge = Math.Clamp((int)riskAssetsPct, 0, 100);
        if (market.IpcaPercentPerYear is > 0 && cashPct > 50)
            notes.Add("Large cash share may lag IPCA over time.");

        // Emergency coverage via schedule expenses
        var monthlyExpense = schedule
            .Where(s => s.ItemType is CashFlowItemType.Expense or CashFlowItemType.Debt or CashFlowItemType.CardInstallment)
            .Sum(s => Math.Abs(s.AmountPerMonth));
        var monthsCovered = monthlyExpense > 0 ? cashBrl / monthlyExpense : (cashBrl > 0 ? 12 : 0);
        var emergency = monthsCovered switch
        {
            >= 6 => 95,
            >= 3 => 75,
            >= 1 => 50,
            _ => 25
        };
        if (monthsCovered < 3 && monthlyExpense > 0)
            notes.Add($"Cash covers ~{monthsCovered:0.0} months of planned expenses — aim for 3–6.");

        // Goal progress
        var activeGoals = goals.Where(g => !g.IsCompleted && g.TargetAmount > 0).ToList();
        int goalScore;
        if (activeGoals.Count == 0)
        {
            goalScore = 50;
            notes.Add("No active goals — set net-worth or emergency targets to track progress.");
        }
        else
        {
            goalScore = (int)Math.Round(activeGoals.Average(g => Math.Clamp(g.CurrentAmount / g.TargetAmount * 100, 0, 100)));
        }

        var score = (int)Math.Round((liquidity + diversification + inflationHedge + emergency + goalScore) / 5.0);
        return new FinancialHealthScore
        {
            Score = score,
            Grade = Grade(score),
            LiquidityScore = liquidity,
            DiversificationScore = diversification,
            InflationHedgeScore = inflationHedge,
            EmergencyCoverageScore = emergency,
            GoalProgressScore = goalScore,
            Notes = notes
        };
    }

    private static PortfolioScore BuildPortfolioScore(
        List<HoldingPerformanceItem> holdings,
        List<AllocationSlice> byType,
        decimal variableBrl,
        decimal totalBrl,
        decimal estimatedAnnual,
        MarketDataResponse market)
    {
        var notes = new List<string>();
        var distinct = byType.Count;
        var top = holdings.FirstOrDefault();
        var topPct = totalBrl > 0 && top != null ? top.MarketValueBrl / totalBrl * 100 : 0;

        var diversification = Math.Clamp(distinct * 18 + (topPct < 25 ? 25 : topPct < 40 ? 10 : 0), 0, 100);
        var variablePct = totalBrl > 0 ? variableBrl / totalBrl * 100 : 0;
        // Risk score: higher = better balanced risk (not max risk). Ideal variable 20–50%.
        var risk = variablePct switch
        {
            >= 20 and <= 50 => 90,
            >= 10 and < 20 => 75,
            > 50 and <= 70 => 70,
            > 70 => 45,
            _ => 55
        };
        if (variablePct > 70) notes.Add("Very high variable-income share — expect larger drawdowns.");
        if (variablePct < 5 && totalBrl > 0) notes.Add("Almost no variable income — long-term growth may be limited.");

        var ipca = market.IpcaPercentPerYear ?? 4.5m;
        var longTerm = estimatedAnnual >= ipca + 2 ? 85 : estimatedAnnual >= ipca ? 65 : 40;
        if (estimatedAnnual < ipca)
            notes.Add("Estimated deterministic yield is below IPCA — real returns may be negative.");

        var score = (int)Math.Round((diversification + risk + longTerm) / 3.0);
        return new PortfolioScore
        {
            Score = score,
            Grade = Grade(score),
            DiversificationScore = diversification,
            RiskScore = risk,
            LongTermFitScore = longTerm,
            ConcentrationTopHoldingPercent = Math.Round(topPct, 1),
            DistinctAssetClasses = distinct,
            Notes = notes
        };
    }

    private static List<string> BuildInsights(
        FinancialHealthScore health,
        PortfolioScore portfolio,
        List<BenchmarkComparisonItem> benchmarks,
        List<RebalanceSuggestion> rebalance,
        decimal passiveYtd,
        decimal unrealized)
    {
        var tips = new List<string>
        {
            $"Financial health score: {health.Score}/100 ({health.Grade}). Portfolio score: {portfolio.Score}/100 ({portfolio.Grade})."
        };

        tips.AddRange(health.Notes.Take(2));
        tips.AddRange(portfolio.Notes.Take(2));

        var cdi = benchmarks.FirstOrDefault(b => b.Code == "CDI");
        if (cdi != null)
        {
            tips.Add(cdi.PortfolioBeats
                ? $"Estimated portfolio yield beats CDI by {cdi.DifferencePercent:0.0}pp."
                : $"Estimated portfolio yield trails CDI by {Math.Abs(cdi.DifferencePercent):0.0}pp.");
        }

        if (passiveYtd > 0)
            tips.Add($"Passive income YTD: R$ {passiveYtd:N2}.");
        if (unrealized != 0)
            tips.Add($"Unrealized P&L (variable income with cost basis): R$ {unrealized:N2}.");

        tips.AddRange(rebalance.Take(2).Select(r => r.Message));
        return tips.Distinct().Take(8).ToList();
    }

    private static string Grade(int score) => score switch
    {
        >= 90 => "A",
        >= 80 => "B",
        >= 70 => "C",
        >= 60 => "D",
        _ => "E"
    };

    private static PortfolioSnapshotResponse MapSnapshot(PortfolioSnapshot s) => new()
    {
        Id = s.Id,
        SnapshotDate = s.SnapshotDate,
        TotalAmountBrl = s.TotalAmountBrl,
        CashAmountBrl = s.CashAmountBrl,
        FixedIncomeAmountBrl = s.FixedIncomeAmountBrl,
        VariableIncomeAmountBrl = s.VariableIncomeAmountBrl,
        UnrealizedGainBrl = s.UnrealizedGainBrl,
        InvestmentCount = s.InvestmentCount,
        Notes = s.Notes
    };
}
