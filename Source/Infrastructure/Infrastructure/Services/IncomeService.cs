using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Domain;

namespace WealthLedger.Infrastructure.Services;

public class IncomeService : IIncomeService
{
    private readonly IRepository<IncomeProfile> _profileRepo;
    private readonly IRepository<ExtraIncome> _extraRepo;
    private readonly IRepository<BusinessDayOverride> _overrideRepo;

    public IncomeService(
        IRepository<IncomeProfile> profileRepo,
        IRepository<ExtraIncome> extraRepo,
        IRepository<BusinessDayOverride> overrideRepo)
    {
        _profileRepo = profileRepo;
        _extraRepo = extraRepo;
        _overrideRepo = overrideRepo;
    }

    // ─── Profile ─────────────────────────────────────────────────

    public async Task<IncomeProfile?> GetProfileAsync()
    {
        var all = await _profileRepo.GetAllAsync();
        return all.FirstOrDefault();
    }

    public async Task<IncomeProfile> SaveProfileAsync(IncomeProfile profile)
    {
        var existing = await _profileRepo.GetAllAsync();
        foreach (var old in existing)
            await _profileRepo.DeleteAsync(old.Id);

        profile.Id = Guid.NewGuid();
        profile.CreatedDate = profile.UpdatedDate = DateTime.UtcNow;
        return await _profileRepo.UpsertAsync(profile);
    }

    // ─── Extra income ────────────────────────────────────────────

    public async Task<IEnumerable<ExtraIncome>> GetExtraIncomeAsync()
    {
        var all = await _extraRepo.GetAllAsync();
        return all.OrderBy(e => e.Year).ThenBy(e => e.Month);
    }

    public async Task<ExtraIncome> AddExtraIncomeAsync(ExtraIncome entry)
    {
        entry.Id = Guid.NewGuid();
        entry.CreatedDate = entry.UpdatedDate = DateTime.UtcNow;
        return await _extraRepo.UpsertAsync(entry);
    }

    public Task DeleteExtraIncomeAsync(Guid id) => _extraRepo.DeleteAsync(id);

    // ─── Business day overrides ──────────────────────────────────

    public async Task SetBusinessDayOverrideAsync(int year, int month, int days)
    {
        var all = await _overrideRepo.GetAllAsync();
        var existing = all.FirstOrDefault(o => o.Year == year && o.Month == month);

        if (existing != null)
        {
            existing.Days = days;
            existing.UpdatedDate = DateTime.UtcNow;
            await _overrideRepo.UpsertAsync(existing);
        }
        else
        {
            await _overrideRepo.UpsertAsync(new BusinessDayOverride
            {
                Id = Guid.NewGuid(),
                Year = year,
                Month = month,
                Days = days,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow,
            });
        }
    }

    public async Task ResetBusinessDayOverrideAsync(int year, int month)
    {
        var all = await _overrideRepo.GetAllAsync();
        var existing = all.FirstOrDefault(o => o.Year == year && o.Month == month);
        if (existing != null)
            await _overrideRepo.DeleteAsync(existing.Id);
    }

    public async Task<Dictionary<(int year, int month), int>> GetAllOverridesAsync()
    {
        var all = await _overrideRepo.GetAllAsync();
        return all.ToDictionary(o => (o.Year, o.Month), o => o.Days);
    }

    // ─── Business days calculation ───────────────────────────────

    public int GetDefaultBusinessDaysInMonth(int year, int month)
    {
        var holidays = GetBrazilianHolidays(year);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        int count = 0;
        for (int d = 1; d <= daysInMonth; d++)
        {
            var date = new DateTime(year, month, d);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                continue;
            if (holidays.Contains(date))
                continue;
            count++;
        }
        return count;
    }

    public async Task<int> GetEffectiveBusinessDaysAsync(int year, int month)
    {
        var all = await _overrideRepo.GetAllAsync();
        var ov = all.FirstOrDefault(o => o.Year == year && o.Month == month);
        return ov?.Days ?? GetDefaultBusinessDaysInMonth(year, month);
    }

    public async Task<(decimal gross, decimal tax, decimal net)> ComputeMonthlyIncomeAsync(
        int year, int month, IncomeProfile profile)
    {
        var days = await GetEffectiveBusinessDaysAsync(year, month);
        var grossBrl = days * profile.HoursPerDay * profile.HourlyRateUsd * profile.UsdBrlRate;
        var taxBrl = Math.Round(grossBrl * profile.TaxPercent / 100m, 2);
        var netBrl = Math.Round(grossBrl - taxBrl, 2);
        grossBrl = Math.Round(grossBrl, 2);
        return (grossBrl, taxBrl, netBrl);
    }

    // ─── Brazilian national holidays ─────────────────────────────

    private static HashSet<DateTime> GetBrazilianHolidays(int year)
    {
        var holidays = new HashSet<DateTime>
        {
            new(year, 1, 1),   // Confraternização Universal
            new(year, 4, 21),  // Tiradentes
            new(year, 5, 1),   // Dia do Trabalho
            new(year, 9, 7),   // Independência do Brasil
            new(year, 10, 12), // Nossa Senhora Aparecida
            new(year, 11, 2),  // Finados
            new(year, 11, 15), // Proclamação da República
            new(year, 12, 25), // Natal
        };

        var easter = ComputeEaster(year);
        holidays.Add(easter.AddDays(-48)); // Carnival Monday
        holidays.Add(easter.AddDays(-47)); // Carnival Tuesday
        holidays.Add(easter.AddDays(-2));  // Good Friday (Sexta-feira Santa)
        holidays.Add(easter.AddDays(60));  // Corpus Christi

        return holidays;
    }

    /// <summary>
    /// Anonymous Gregorian algorithm for computing Easter Sunday.
    /// </summary>
    private static DateTime ComputeEaster(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateTime(year, month, day);
    }
}
