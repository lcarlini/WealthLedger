using WealthLedger.Contracts.Domain;

namespace WealthLedger.Application.Services;

public interface IInvestmentService : IEntityService<Investment>
{
    Task<IEnumerable<Investment>> GetByInstitutionIdAsync(Guid institutionId);
}
