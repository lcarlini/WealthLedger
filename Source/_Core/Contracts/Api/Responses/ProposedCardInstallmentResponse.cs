namespace WealthLedger.Contracts.Api.Responses;

public class ProposedCardInstallmentResponse
{
    public Guid BankTransactionId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal AmountPerMonth { get; set; }
    public int StartYear { get; set; }
    public int StartMonth { get; set; }
    public int RemainingInstallments { get; set; }
    public int InstallmentNumber { get; set; }
    public int InstallmentTotal { get; set; }
}
