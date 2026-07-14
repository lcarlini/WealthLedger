using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Queries;
using WealthLedger.Contracts.Domain.Interfaces;
using WealthLedger.Contracts.Domain.Pagination;

namespace WealthLedger.Infrastructure.Services;

public class EntityService<T> : IEntityService<T> where T : class, IEntity
{
    private readonly IRepository<T> _repository;

    public EntityService(IRepository<T> repository)
    {
        _repository = repository;
    }

    public Task<IEnumerable<T>> GetAllAsync() => _repository.GetAllAsync();

    public Task<PagedResponse<T>> GetPagedAsync(QueryOptions query) => _repository.GetPagedAsync(query);

    public Task<T?> GetAsync(Guid id) => _repository.GetAsync(id);

    public Task<T> UpsertAsync(T entity) => _repository.UpsertAsync(entity);

    public Task DeleteAsync(Guid id) => _repository.DeleteAsync(id);

    public Task<bool> CheckDuplicateAsync(string fieldName, string fieldValue, Guid? excludeEntityId = null) =>
        _repository.ExistsAsync(fieldName, fieldValue, excludeEntityId);
}
