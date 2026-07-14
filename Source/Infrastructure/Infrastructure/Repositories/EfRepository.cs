// TODO: SQLLITE - This repository uses EF Core + SQLite instead of InMemoryRepository.
// It is registered in Services.cs (commented out by default).

using System.Reflection;
using WealthLedger.Application.Repositories;
using WealthLedger.Contracts.Api.Queries;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Interfaces;
using WealthLedger.Contracts.Domain.Pagination;
using WealthLedger.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace WealthLedger.Infrastructure.Repositories;

public class EfRepository<T> : IRepository<T> where T : BaseEntity
{
    private readonly WealthLedgerDbContext _context;
    private readonly DbSet<T> _dbSet;

    public EfRepository(WealthLedgerDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.OrderBy(e => e.CreatedDate).ToListAsync();
    }

    public async Task<PagedResponse<T>> GetPagedAsync(QueryOptions query)
    {
        IQueryable<T> items = _dbSet;

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLowerInvariant();
            var nameProperty = typeof(T).GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
            if (nameProperty != null)
            {
                items = items.Where(e => EF.Property<string>(e, "Name").ToLower().Contains(search));
            }
        }

        var totalCount = await items.CountAsync();

        items = query.SortDescending
            ? items.OrderByDescending(e => e.CreatedDate)
            : items.OrderBy(e => e.CreatedDate);

        var result = await items
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        return new PagedResponse<T>(query.Page, query.PageSize, totalCount, result);
    }

    public async Task<T?> GetAsync(Guid id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<T> UpsertAsync(T entity)
    {
        var existing = await _dbSet.FindAsync(entity.Id);

        if (existing == null || entity.Id == Guid.Empty)
        {
            if (entity.Id == Guid.Empty)
                entity.Id = Guid.NewGuid();

            entity.CreatedDate = DateTime.UtcNow;
            entity.UpdatedDate = DateTime.UtcNow;
            await _dbSet.AddAsync(entity);
        }
        else
        {
            entity.CreatedDate = existing.CreatedDate;
            entity.UpdatedDate = DateTime.UtcNow;
            _context.Entry(existing).CurrentValues.SetValues(entity);
        }

        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _dbSet.FindAsync(id);
        if (entity != null)
        {
            _dbSet.Remove(entity);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(string fieldName, string fieldValue, Guid? excludeEntityId = null)
    {
        var property = typeof(T).GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property == null) return false;

        IQueryable<T> query = _dbSet;

        if (excludeEntityId.HasValue)
            query = query.Where(e => e.Id != excludeEntityId.Value);

        return await query.AnyAsync(e =>
            EF.Property<string>(e, fieldName).ToLower() == fieldValue.ToLowerInvariant());
    }
}
