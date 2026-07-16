namespace WealthLedger.Contracts.Api.Responses;

public class PortfolioOverviewResponse
{
    public decimal TotalNetWorthBrl { get; set; }
    public decimal TotalCostBasisBrl { get; set; }
    public decimal UnrealizedGainBrl { get; set; }
    public decimal UnrealizedGainPercent { get; set; }
    public decimal RealizedPassiveIncomeBrl { get; set; }
    public decimal RealizedPassiveIncomeYtdBrl { get; set; }
    public decimal CashAmountBrl { get; set; }
    public decimal FixedIncomeAmountBrl { get; set; }
    public decimal VariableIncomeAmountBrl { get; set; }
    public int InvestmentCount { get; set; }
    public int InstitutionCount { get; set; }

    public FinancialHealthScore Health { get; set; } = new();
    public PortfolioScore PortfolioScore { get; set; } = new();
    public List<AllocationSlice> AllocationByType { get; set; } = [];
    public List<AllocationSlice> AllocationByCurrency { get; set; } = [];
    public List<AllocationSlice> AllocationByInstitution { get; set; } = [];
    public List<HoldingPerformanceItem> Holdings { get; set; } = [];
    public List<BenchmarkComparisonItem> Benchmarks { get; set; } = [];
    public List<RebalanceSuggestion> RebalanceSuggestions { get; set; } = [];
    public List<CalendarEventItem> Calendar { get; set; } = [];
    public List<PortfolioSnapshotResponse> Timeline { get; set; } = [];
    public List<string> Insights { get; set; } = [];
}

public class FinancialHealthScore
{
    public int Score { get; set; }
    public string Grade { get; set; } = "C";
    public int LiquidityScore { get; set; }
    public int DiversificationScore { get; set; }
    public int InflationHedgeScore { get; set; }
    public int EmergencyCoverageScore { get; set; }
    public int GoalProgressScore { get; set; }
    public List<string> Notes { get; set; } = [];
}

public class PortfolioScore
{
    public int Score { get; set; }
    public string Grade { get; set; } = "C";
    public int DiversificationScore { get; set; }
    public int RiskScore { get; set; }
    public int LongTermFitScore { get; set; }
    public decimal ConcentrationTopHoldingPercent { get; set; }
    public int DistinctAssetClasses { get; set; }
    public List<string> Notes { get; set; } = [];
}

public class AllocationSlice
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal AmountBrl { get; set; }
    public decimal Percent { get; set; }
    public int Count { get; set; }
}

public class HoldingPerformanceItem
{
    public Guid InvestmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? InstitutionName { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public string Currency { get; set; } = "BRL";
    public string? Ticker { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? AveragePrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal MarketValueBrl { get; set; }
    public decimal? CostBasis { get; set; }
    public decimal? CostBasisBrl { get; set; }
    public decimal? UnrealizedGain { get; set; }
    public decimal? UnrealizedGainBrl { get; set; }
    public decimal? UnrealizedGainPercent { get; set; }
    public bool IsVariableIncome { get; set; }
}

public class BenchmarkComparisonItem
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal AnnualRatePercent { get; set; }
    public decimal PortfolioEstimatedAnnualPercent { get; set; }
    public decimal DifferencePercent { get; set; }
    public bool PortfolioBeats { get; set; }
    public string PeriodLabel { get; set; } = "Estimated a.a.";
}

public class RebalanceSuggestion
{
    public string Category { get; set; } = string.Empty;
    public decimal CurrentPercent { get; set; }
    public decimal TargetPercent { get; set; }
    public decimal DriftPercent { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class CalendarEventItem
{
    public DateTime Date { get; set; }
    public string Title { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? InvestmentName { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }
}
