using WealthLedger.Contracts.Api.Budgets;
using WealthLedger.Contracts.Domain;

namespace WealthLedger.Application.Services;

public interface IBudgetService
{
    Task<IReadOnlyList<Budget>> GetAllAsync();

    /// <summary>Creates a budget, or updates the limit when the category already has one.</summary>
    /// <exception cref="ArgumentException">Blank category, non-positive or absurd limit.</exception>
    Task<Budget> SaveAsync(BudgetRequest request);

    Task DeleteAsync(Guid id);

    /// <summary>Budgets versus actual spending for one calendar month.</summary>
    /// <exception cref="ArgumentException">Month outside 1-12.</exception>
    Task<BudgetStatus> GetStatusAsync(int year, int month);

    /// <summary>Categories seen in imported transactions plus the built-in ones, to suggest when adding a budget.</summary>
    Task<IReadOnlyList<string>> GetKnownCategoriesAsync();
}
