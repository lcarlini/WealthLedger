namespace WealthLedger.Contracts.Domain;

/// <summary>
/// Stores a per-month override for business day count.
/// When present, overrides the auto-calculated value (which considers
/// weekends and Brazilian national holidays).
/// </summary>
public class BusinessDayOverride : BaseEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Days { get; set; }
}
