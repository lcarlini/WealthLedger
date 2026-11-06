namespace WealthLedger.Contracts.Domain;

/// <summary>A monthly spending limit for one expense category (matches the categories of imported transactions).</summary>
public class Budget : BaseEntity
{
    public string Category { get; set; } = string.Empty;
    public decimal MonthlyLimit { get; set; }
}
