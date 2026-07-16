using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Contracts.Domain;

/// <summary>Recorded dividend, interest, JCP, or other passive income event.</summary>
public class PassiveIncome : BaseEntity
{
    public Guid? InvestmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public PassiveIncomeType IncomeType { get; set; } = PassiveIncomeType.Dividend;
    public Currency Currency { get; set; } = Currency.BRL;
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; }
    public string? Notes { get; set; }
}
