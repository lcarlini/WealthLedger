using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Contracts.Domain;

/// <summary>
/// Recurring bill / payment reminder definition (e.g. school tuition due on the 10th every month).
/// </summary>
public class BillPaymentAlert : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal Amount { get; set; }
    public Currency Currency { get; set; } = Currency.BRL;
    /// <summary>Day of month the bill is due (1–31; clamped to month length when projecting).</summary>
    public int DueDay { get; set; } = 1;
    public BillRecurrence Recurrence { get; set; } = BillRecurrence.Monthly;
    public int StartYear { get; set; }
    public int StartMonth { get; set; }
    public int? EndYear { get; set; }
    public int? EndMonth { get; set; }
    public bool IsActive { get; set; } = true;
}
