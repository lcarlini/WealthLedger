using WealthLedger.Contracts.Domain.Enums;
using WealthLedger.Contracts.Domain.Interfaces;

namespace WealthLedger.Contracts.Api.Requests;

public class CashFlowScheduleItemRequestBody : IEntityRequestBody
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
}
