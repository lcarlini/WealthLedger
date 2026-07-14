using WealthLedger.Contracts.Domain;

namespace WealthLedger.Application.Services;

public interface IIncomeService
{
    Task<IncomeProfile?> GetProfileAsync();
    Task<IncomeProfile> SaveProfileAsync(IncomeProfile profile);

    Task<IEnumerable<ExtraIncome>> GetExtraIncomeAsync();
    Task<ExtraIncome> AddExtraIncomeAsync(ExtraIncome entry);
    Task DeleteExtraIncomeAsync(Guid id);

    /// <summary>
    /// Returns the calculated number of business days (weekdays minus Brazilian holidays)
    /// for the given month/year, ignoring any override.
    /// </summary>
    int GetDefaultBusinessDaysInMonth(int year, int month);

    /// <summary>
    /// Returns the effective business days for a month: uses the override if one exists,
    /// otherwise falls back to the default calculated value.
    /// </summary>
    Task<int> GetEffectiveBusinessDaysAsync(int year, int month);

    /// <summary>
    /// Sets a business day override for a given month.
    /// </summary>
    Task SetBusinessDayOverrideAsync(int year, int month, int days);

    /// <summary>
    /// Removes the business day override for a given month, reverting to the calculated value.
    /// </summary>
    Task ResetBusinessDayOverrideAsync(int year, int month);

    /// <summary>
    /// Returns all stored overrides as a dictionary keyed by (year, month).
    /// </summary>
    Task<Dictionary<(int year, int month), int>> GetAllOverridesAsync();

    /// <summary>
    /// Computes gross, tax, and net income for a given month using the income profile
    /// and effective business days (override-aware). Returns (gross, tax, net) in BRL.
    /// </summary>
    Task<(decimal gross, decimal tax, decimal net)> ComputeMonthlyIncomeAsync(int year, int month, IncomeProfile profile);
}
