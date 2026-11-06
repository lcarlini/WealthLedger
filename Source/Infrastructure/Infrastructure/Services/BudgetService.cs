using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Budgets;
using WealthLedger.Contracts.Domain;

namespace WealthLedger.Infrastructure.Services;

public sealed class BudgetService(
    IRepository<Budget> budgets,
    IRepository<BankTransaction> transactions) : IBudgetService
{
    public const decimal MaxLimit = 1_000_000_000m;
    public const decimal WarningPercent = 80m;

    public async Task<IReadOnlyList<Budget>> GetAllAsync()
        => (await budgets.GetAllAsync()).OrderBy(b => b.Category, StringComparer.CurrentCultureIgnoreCase).ToList();

    public async Task<Budget> SaveAsync(BudgetRequest request)
    {
        var category = request.Category?.Trim();
        if (string.IsNullOrEmpty(category))
            throw new ArgumentException("The category is required.", nameof(request));
        if (category.Length > 60)
            throw new ArgumentException("The category is too long (max 60 characters).", nameof(request));
        if (request.MonthlyLimit <= 0 || request.MonthlyLimit > MaxLimit)
            throw new ArgumentException("The monthly limit must be greater than zero.", nameof(request));

        var existing = (await budgets.GetAllAsync())
            .FirstOrDefault(b => string.Equals(b.Category, category, StringComparison.OrdinalIgnoreCase));

        var entity = existing ?? new Budget { Category = category };
        entity.MonthlyLimit = Math.Round(request.MonthlyLimit, 2);
        return await budgets.UpsertAsync(entity);
    }

    public Task DeleteAsync(Guid id) => budgets.DeleteAsync(id);

    public async Task<BudgetStatus> GetStatusAsync(int year, int month)
    {
        if (month is < 1 or > 12 || year is < 1900 or > 2200)
            throw new ArgumentException("Month must be 1-12 and year a valid year.");

        var all = await GetAllAsync();
        var spending = (await transactions.GetAllAsync())
            .Where(t => t.Amount < 0 && t.Date.Year == year && t.Date.Month == month);
        return Evaluate(all, spending, year, month);
    }

    public async Task<IReadOnlyList<string>> GetKnownCategoriesAsync()
    {
        var seen = (await transactions.GetAllAsync())
            .Select(t => t.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!);
        return OfxImportService.KnownCategories
            .Concat(seen)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(c => !string.Equals(c, "Income", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>Pure calculation, kept static so it can be tested without a database.</summary>
    public static BudgetStatus Evaluate(
        IEnumerable<Budget> budgetList, IEnumerable<BankTransaction> monthExpenses, int year, int month)
    {
        var spentByCategory = monthExpenses
            .GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Other" : t.Category!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(t => -t.Amount), StringComparer.OrdinalIgnoreCase);

        var items = new List<BudgetItemStatus>();
        foreach (var b in budgetList)
        {
            var spent = spentByCategory.GetValueOrDefault(b.Category);
            var percent = b.MonthlyLimit > 0 ? Math.Round(spent / b.MonthlyLimit * 100m, 1) : 0m;
            var level = percent > 100m ? BudgetLevels.Over
                : percent >= WarningPercent ? BudgetLevels.Warning
                : BudgetLevels.Ok;
            items.Add(new BudgetItemStatus(
                b.Id, b.Category, b.MonthlyLimit, Math.Round(spent, 2),
                Math.Round(b.MonthlyLimit - spent, 2), percent, level));
        }

        var budgeted = items.Select(i => i.Category).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unbudgeted = spentByCategory
            .Where(kv => !budgeted.Contains(kv.Key))
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new CategorySpending(kv.Key, Math.Round(kv.Value, 2)))
            .ToList();

        return new BudgetStatus(
            year, month, items, unbudgeted,
            TotalLimit: items.Sum(i => i.MonthlyLimit),
            TotalSpent: Math.Round(spentByCategory.Values.Sum(), 2));
    }
}
