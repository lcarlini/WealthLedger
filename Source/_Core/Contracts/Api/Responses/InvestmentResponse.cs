using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Contracts.Api.Responses;

public class InvestmentResponse
{
    public Guid Id { get; set; }
    public Guid FinancialInstitutionId { get; set; }
    public string? InstitutionName { get; set; }
    public string Name { get; set; } = string.Empty;
    public AccountType AccountType { get; set; }
    public Currency Currency { get; set; }
    public decimal Amount { get; set; }
    public decimal CdiPercentage { get; set; }
    public decimal? AnnualRatePercent { get; set; }
    public DateTime? MaturityDate { get; set; }
    public bool RequiresMonthlyMovement { get; set; }
    public decimal? MonthlyMovementAmount { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}
