namespace WealthLedger.Contracts.Api.Budgets;

public sealed record BudgetRequest(string Category, decimal MonthlyLimit);

/// <summary>How far a budget is used: ok below 80%, warning from 80% to 100%, over above 100%.</summary>
public static class BudgetLevels
{
    public const string Ok = "ok";
    public const string Warning = "warning";
    public const string Over = "over";
}

public sealed record BudgetItemStatus(
    Guid Id,
    string Category,
    decimal MonthlyLimit,
    decimal Spent,
    decimal Remaining,
    decimal PercentUsed,
    string Level);

public sealed record CategorySpending(string Category, decimal Amount);

/// <param name="Unbudgeted">Spending in categories that have no budget yet, largest first.</param>
public sealed record BudgetStatus(
    int Year,
    int Month,
    IReadOnlyList<BudgetItemStatus> Items,
    IReadOnlyList<CategorySpending> Unbudgeted,
    decimal TotalLimit,
    decimal TotalSpent);
