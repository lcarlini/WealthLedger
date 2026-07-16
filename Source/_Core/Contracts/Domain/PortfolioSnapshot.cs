namespace WealthLedger.Contracts.Domain;

/// <summary>Point-in-time net worth snapshot for historical timeline charts.</summary>
public class PortfolioSnapshot : BaseEntity
{
    public DateTime SnapshotDate { get; set; }
    public decimal TotalAmountBrl { get; set; }
    public decimal CashAmountBrl { get; set; }
    public decimal FixedIncomeAmountBrl { get; set; }
    public decimal VariableIncomeAmountBrl { get; set; }
    public decimal UnrealizedGainBrl { get; set; }
    public int InvestmentCount { get; set; }
    public string? Notes { get; set; }
}
