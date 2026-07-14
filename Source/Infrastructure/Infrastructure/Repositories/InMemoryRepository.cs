using System.Collections.Concurrent;
using System.Reflection;
using WealthLedger.Application.Repositories;
using WealthLedger.Contracts.Api.Queries;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Interfaces;
using WealthLedger.Contracts.Domain.Pagination;

namespace WealthLedger.Infrastructure.Repositories;

public class InMemoryRepository<T> : IRepository<T> where T : BaseEntity
{
    private readonly ConcurrentDictionary<Guid, T> _store = new();

    public Task<IEnumerable<T>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<T>>(_store.Values.OrderBy(e => e.CreatedDate));
    }

    public Task<PagedResponse<T>> GetPagedAsync(QueryOptions query)
    {
        IEnumerable<T> items = _store.Values;

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLowerInvariant();
            items = items.Where(e =>
            {
                var nameProperty = typeof(T).GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                var nameValue = nameProperty?.GetValue(e)?.ToString()?.ToLowerInvariant();
                return nameValue?.Contains(search) == true;
            });
        }

        var totalCount = items.Count();

        items = query.SortDescending
            ? items.OrderByDescending(e => e.CreatedDate)
            : items.OrderBy(e => e.CreatedDate);

        items = items
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize);

        var result = new PagedResponse<T>(query.Page, query.PageSize, totalCount, items.ToList());
        return Task.FromResult(result);
    }

    public Task<T?> GetAsync(Guid id)
    {
        _store.TryGetValue(id, out var entity);
        return Task.FromResult(entity);
    }

    public Task<T> UpsertAsync(T entity)
    {
        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
            entity.CreatedDate = DateTime.UtcNow;
        }

        entity.UpdatedDate = DateTime.UtcNow;
        _store[entity.Id] = entity;
        return Task.FromResult(entity);
    }

    public Task DeleteAsync(Guid id)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string fieldName, string fieldValue, Guid? excludeEntityId = null)
    {
        var property = typeof(T).GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property == null) return Task.FromResult(false);

        var exists = _store.Values.Any(e =>
        {
            if (excludeEntityId.HasValue && e.Id == excludeEntityId.Value) return false;
            var value = property.GetValue(e)?.ToString();
            return string.Equals(value, fieldValue, StringComparison.OrdinalIgnoreCase);
        });

        return Task.FromResult(exists);
    }
}
