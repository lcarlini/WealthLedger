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

    /// <summary>When the principal was applied (holding-period start).</summary>
    public DateTime StartDate { get; set; }

    /// <summary>Original amount applied. Cost basis for fixed income; for variable income often Qty×AvgPrice.</summary>
    public decimal PrincipalAmount { get; set; }

    /// <summary>Active positions appear in dashboards; Archived keep history after redemption.</summary>
    public InvestmentStatus Status { get; set; } = InvestmentStatus.Active;

    /// <summary>When the position was archived (redeemed/closed).</summary>
    public DateTime? ArchivedAt { get; set; }

    /// <summary>Amount received on redemption (gross of tax withholding when known).</summary>
    public decimal? RedeemedAmount { get; set; }

    /// <summary>Optional note on why/how it was closed.</summary>
    public string? ArchiveNotes { get; set; }

    /// <summary>Book Amount captured immediately before archive overwrite.</summary>
    public decimal? BookAmountAtArchive { get; set; }

    /// <summary>Index/rate theoretical value at archive time (CDI%/annual rate accrual).</summary>
    public decimal? ExpectedTheoreticalAtArchive { get; set; }

    /// <summary>True for LCI/LCA/Poupança-style products (no IR on gains for PF).</summary>
    public bool IsIncomeTaxExempt { get; set; }
}
