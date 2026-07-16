using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Contracts.Api.Responses;

public class PassiveIncomeResponse
{
    public Guid Id { get; set; }
    public Guid? InvestmentId { get; set; }
    public string? InvestmentName { get; set; }
    public string Name { get; set; } = string.Empty;
    public PassiveIncomeType IncomeType { get; set; }
    public Currency Currency { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}

public class InvestmentGoalResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public GoalType GoalType { get; set; }
    public Currency Currency { get; set; }
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateTime? TargetDate { get; set; }
    public decimal? MonthlyContribution { get; set; }
    public decimal? ExpectedAnnualReturnPercent { get; set; }
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; }
    public decimal ProgressPercent { get; set; }
    public decimal RemainingAmount { get; set; }
    public int? MonthsRemaining { get; set; }
    public decimal? ProjectedAmountAtTarget { get; set; }
    public bool? OnTrack { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}

public class WatchlistItemResponse
{
    public Guid Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public decimal? TargetPrice { get; set; }
    public decimal? AlertAbove { get; set; }
    public decimal? AlertBelow { get; set; }
    public string? Notes { get; set; }
    public decimal? LastPrice { get; set; }
    public string? LastPriceCurrency { get; set; }
    public bool? AlertTriggered { get; set; }
    public string? AlertMessage { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}

public class PortfolioSnapshotResponse
{
    public Guid Id { get; set; }
    public DateTime SnapshotDate { get; set; }
    public decimal TotalAmountBrl { get; set; }
    public decimal CashAmountBrl { get; set; }
    public decimal FixedIncomeAmountBrl { get; set; }
    public decimal VariableIncomeAmountBrl { get; set; }
    public decimal UnrealizedGainBrl { get; set; }
    public int InvestmentCount { get; set; }
    public string? Notes { get; set; }
}
