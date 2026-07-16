using WealthLedger.Contracts.Domain.Enums;
using WealthLedger.Contracts.Domain.Interfaces;

namespace WealthLedger.Contracts.Api.Requests;

public class InvestmentRequestBody : IEntityRequestBody
{
    public Guid Id { get; set; }
    public Guid FinancialInstitutionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public Currency Currency { get; set; } = Currency.BRL;
    public decimal Amount { get; set; }
    public decimal CdiPercentage { get; set; }
    public decimal? AnnualRatePercent { get; set; }
    public DateTime? MaturityDate { get; set; }
    public bool RequiresMonthlyMovement { get; set; }
    public decimal? MonthlyMovementAmount { get; set; }
    public string? Ticker { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? AveragePrice { get; set; }
}
