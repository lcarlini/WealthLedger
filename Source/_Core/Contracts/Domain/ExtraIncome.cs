namespace WealthLedger.Contracts.Domain;

/// <summary>
/// An ad-hoc extra income entry for a specific month (e.g. bonus, freelance).
/// </summary>
public class ExtraIncome : BaseEntity
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
}
