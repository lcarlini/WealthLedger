namespace WealthLedger.Contracts.Domain;

public class BankTransaction : BaseEntity
{
    public Guid StatementImportId { get; set; }
    public string? FitId { get; set; }
    public string TransactionType { get; set; } = string.Empty; // DEBIT, CREDIT, etc.
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int? InstallmentNumber { get; set; }
    public int? InstallmentTotal { get; set; }
}
