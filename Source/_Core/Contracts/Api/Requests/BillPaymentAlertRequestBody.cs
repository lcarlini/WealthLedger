using WealthLedger.Contracts.Domain.Enums;
using WealthLedger.Contracts.Domain.Interfaces;

namespace WealthLedger.Contracts.Api.Requests;

public class BillPaymentAlertRequestBody : IEntityRequestBody
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal Amount { get; set; }
    public Currency Currency { get; set; } = Currency.BRL;
    public int DueDay { get; set; } = 1;
    public BillRecurrence Recurrence { get; set; } = BillRecurrence.Monthly;
    public int StartYear { get; set; }
    public int StartMonth { get; set; }
    public int? EndYear { get; set; }
    public int? EndMonth { get; set; }
    public bool IsActive { get; set; } = true;
}
