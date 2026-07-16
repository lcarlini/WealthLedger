using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Contracts.Domain;

public class Investment : BaseEntity
{
    public Guid FinancialInstitutionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public Currency Currency { get; set; } = Currency.BRL;
    public decimal Amount { get; set; }
    public decimal CdiPercentage { get; set; }
    public decimal? AnnualRatePercent { get; set; }
    public DateTime? MaturityDate { get; set; }
    public bool RequiresMonthlyMovement { get; set; }
    public decimal? MonthlyMovementAmount { get; set; }

    /// <summary>Ticker / symbol for variable-income (e.g. PETR4, HGLG11, IVVB11, BTC).</summary>
    public string? Ticker { get; set; }

    /// <summary>Number of shares, quotas, or units held (variable-income).</summary>
    public decimal? Quantity { get; set; }

    /// <summary>Average acquisition price per unit (variable-income cost basis).</summary>
    public decimal? AveragePrice { get; set; }
}
