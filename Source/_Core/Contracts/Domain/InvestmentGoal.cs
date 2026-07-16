using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Contracts.Domain;

/// <summary>Goal-based investing target (net worth, emergency fund, retirement, custom).</summary>
public class InvestmentGoal : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public GoalType GoalType { get; set; } = GoalType.Custom;
    public Currency Currency { get; set; } = Currency.BRL;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateTime? TargetDate { get; set; }
    public decimal? MonthlyContribution { get; set; }
    public decimal? ExpectedAnnualReturnPercent { get; set; }
    public string? Notes { get; set; }
    public bool IsCompleted { get; set; }
}
