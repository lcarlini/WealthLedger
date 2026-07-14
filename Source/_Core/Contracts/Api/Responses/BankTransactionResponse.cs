namespace WealthLedger.Contracts.Api.Responses;

public class BankTransactionResponse
{
    public Guid Id { get; set; }
    public Guid StatementImportId { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int? InstallmentNumber { get; set; }
    public int? InstallmentTotal { get; set; }
}
