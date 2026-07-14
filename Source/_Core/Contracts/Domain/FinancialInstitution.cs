namespace WealthLedger.Contracts.Domain;

public class FinancialInstitution : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
}
