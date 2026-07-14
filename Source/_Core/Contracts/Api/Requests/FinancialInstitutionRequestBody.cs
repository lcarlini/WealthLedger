using WealthLedger.Contracts.Domain.Interfaces;

namespace WealthLedger.Contracts.Api.Requests;

public class FinancialInstitutionRequestBody : IEntityRequestBody
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
}
