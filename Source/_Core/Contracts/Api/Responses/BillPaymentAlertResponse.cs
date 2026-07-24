namespace WealthLedger.Contracts.Api.Responses;

public class BillPaymentAlertResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BRL";
    public int DueDay { get; set; }
    public string Recurrence { get; set; } = "Monthly";
    public int StartYear { get; set; }
    public int StartMonth { get; set; }
    public int? EndYear { get; set; }
    public int? EndMonth { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}

public class BillPaymentOccurrenceResponse
{
    public Guid Id { get; set; }
    public Guid BillPaymentAlertId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BRL";
    public string Recurrence { get; set; } = "Monthly";
    public int DueYear { get; set; }
    public int DueMonth { get; set; }
    public int DueDay { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
    public bool IsOverdue { get; set; }
    public bool IsVirtual { get; set; }
}
