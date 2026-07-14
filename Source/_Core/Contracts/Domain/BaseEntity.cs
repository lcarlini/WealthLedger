using WealthLedger.Contracts.Domain.Interfaces;

namespace WealthLedger.Contracts.Domain;

public class BaseEntity : IEntity
{
    public Guid Id { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}
