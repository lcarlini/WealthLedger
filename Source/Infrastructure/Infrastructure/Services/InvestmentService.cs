using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Queries;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Pagination;

namespace WealthLedger.Infrastructure.Services;

public class InvestmentService : IInvestmentService
{
    private readonly IRepository<Investment> _repository;

    public InvestmentService(IRepository<Investment> repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<Investment>> GetAllAsync() => _repository.GetAllAsync();
    public Task<PagedResponse<Investment>> GetPagedAsync(QueryOptions query) => _repository.GetPagedAsync(query);
    public Task<Investment?> GetAsync(Guid id) => _repository.GetAsync(id);
    public Task<Investment> UpsertAsync(Investment entity) => _repository.UpsertAsync(entity);
    public Task DeleteAsync(Guid id) => _repository.DeleteAsync(id);

    public Task<bool> CheckDuplicateAsync(string fieldName, string fieldValue, Guid? excludeEntityId = null) =>
        _repository.ExistsAsync(fieldName, fieldValue, excludeEntityId);

    public async Task<IEnumerable<Investment>> GetByInstitutionIdAsync(Guid institutionId)
    {
        var all = await _repository.GetAllAsync();
        return all.Where(i => i.FinancialInstitutionId == institutionId);
    }
}
