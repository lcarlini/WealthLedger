using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Contracts.Api.Responses;

public class CashFlowScheduleItemResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CashFlowItemType ItemType { get; set; }
    public decimal AmountPerMonth { get; set; }
    public int StartYear { get; set; }
    public int StartMonth { get; set; }
    public int NumberOfMonths { get; set; }
    public CashFlowSource Source { get; set; }
    public Guid? BankTransactionId { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedDate { get; set; }
}
