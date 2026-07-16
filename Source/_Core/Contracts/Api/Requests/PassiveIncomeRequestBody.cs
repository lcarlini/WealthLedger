using WealthLedger.Contracts.Domain.Enums;
using WealthLedger.Contracts.Domain.Interfaces;

namespace WealthLedger.Contracts.Api.Requests;

public class PassiveIncomeRequestBody : IEntityRequestBody
{
    public Guid Id { get; set; }
    public Guid? InvestmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public PassiveIncomeType IncomeType { get; set; } = PassiveIncomeType.Dividend;
    public Currency Currency { get; set; } = Currency.BRL;
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? Notes { get; set; }
}
