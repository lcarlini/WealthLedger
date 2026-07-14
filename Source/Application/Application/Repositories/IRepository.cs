using WealthLedger.Contracts.Api.Queries;
using WealthLedger.Contracts.Domain.Interfaces;
using WealthLedger.Contracts.Domain.Pagination;

namespace WealthLedger.Application.Repositories;

public interface IRepository<T> where T : class, IEntity
{
    Task<IEnumerable<T>> GetAllAsync();
    Task<PagedResponse<T>> GetPagedAsync(QueryOptions query);
    Task<T?> GetAsync(Guid id);
    Task<T> UpsertAsync(T entity);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(string fieldName, string fieldValue, Guid? excludeEntityId = null);
}
