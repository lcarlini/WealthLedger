namespace WealthLedger.Contracts.Domain;

/// <summary>
/// A concrete due instance of a <see cref="BillPaymentAlert"/> for a given year/month.
/// </summary>
public class BillPaymentOccurrence : BaseEntity
{
    public Guid BillPaymentAlertId { get; set; }
    public int DueYear { get; set; }
    public int DueMonth { get; set; }
    public int DueDay { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
}
