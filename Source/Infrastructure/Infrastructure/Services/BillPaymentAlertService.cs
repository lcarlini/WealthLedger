using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Infrastructure.Services;

public class BillPaymentAlertService : IBillPaymentAlertService
{
    /// <summary>How many past months to materialize so recent overdue bills still appear.</summary>
    private const int OverdueBackfillMonths = 6;

    private readonly IRepository<BillPaymentAlert> _alertRepository;
    private readonly IRepository<BillPaymentOccurrence> _occurrenceRepository;

    public BillPaymentAlertService(
        IRepository<BillPaymentAlert> alertRepository,
        IRepository<BillPaymentOccurrence> occurrenceRepository)
    {
        _alertRepository = alertRepository;
        _occurrenceRepository = occurrenceRepository;
    }

    public Task<IEnumerable<BillPaymentAlert>> GetAllAsync() => _alertRepository.GetAllAsync();
    public Task<BillPaymentAlert?> GetAsync(Guid id) => _alertRepository.GetAsync(id);

    public Task<BillPaymentAlert> UpsertAsync(BillPaymentAlert entity) =>
        _alertRepository.UpsertAsync(entity);

    public async Task DeleteAsync(Guid id)
    {
        var occurrences = (await _occurrenceRepository.GetAllAsync())
            .Where(o => o.BillPaymentAlertId == id)
            .ToList();
        foreach (var occurrence in occurrences)
            await _occurrenceRepository.DeleteAsync(occurrence.Id);

        await _alertRepository.DeleteAsync(id);
    }

    public async Task<IEnumerable<BillPaymentOccurrenceResponse>> GetDueThisMonthAsync()
    {
        await EnsureOccurrencesAsync();
        var now = DateTime.UtcNow;
        var today = now.Date;
        var alerts = (await _alertRepository.GetAllAsync()).ToDictionary(a => a.Id);
        var occurrences = (await _occurrenceRepository.GetAllAsync())
            .Where(o => !o.IsPaid)
            .Select(o => ToResponse(o, alerts.GetValueOrDefault(o.BillPaymentAlertId), today))
            .Where(r => r != null && (
                (r!.DueYear == now.Year && r.DueMonth == now.Month) ||
                r.IsOverdue))
            .Cast<BillPaymentOccurrenceResponse>()
            .OrderBy(r => r.DueYear)
            .ThenBy(r => r.DueMonth)
            .ThenBy(r => r.DueDay)
            .ThenBy(r => r.Name);

        return occurrences;
    }

    public async Task<IEnumerable<BillPaymentOccurrenceResponse>> GetUpcomingAsync(int monthsAhead = 12)
    {
        await EnsureOccurrencesAsync();
        monthsAhead = Math.Clamp(monthsAhead, 1, 120);
        var now = DateTime.UtcNow;
        var today = now.Date;
        var alerts = (await _alertRepository.GetAllAsync()).Where(a => a.IsActive).ToList();
        var existing = (await _occurrenceRepository.GetAllAsync()).ToList();
        var existingKeys = existing
            .Select(o => (o.BillPaymentAlertId, o.DueYear, o.DueMonth))
            .ToHashSet();

        var results = new List<BillPaymentOccurrenceResponse>();

        // Unpaid future occurrences already materialized (rare; usually only current month)
        foreach (var o in existing.Where(x => !x.IsPaid))
        {
            if (o.DueYear > now.Year || (o.DueYear == now.Year && o.DueMonth > now.Month))
            {
                var response = ToResponse(o, alerts.FirstOrDefault(a => a.Id == o.BillPaymentAlertId), today);
                if (response != null)
                    results.Add(response);
            }
        }

        // Virtual projections for future months
        var cursor = new DateTime(now.Year, now.Month, 1).AddMonths(1);
        var end = new DateTime(now.Year, now.Month, 1).AddMonths(monthsAhead);
        while (cursor <= end)
        {
            foreach (var alert in alerts)
            {
                if (!AppliesInMonth(alert, cursor.Year, cursor.Month))
                    continue;
                if (existingKeys.Contains((alert.Id, cursor.Year, cursor.Month)))
                    continue;

                results.Add(ToVirtualResponse(alert, cursor.Year, cursor.Month, today));
            }

            cursor = cursor.AddMonths(1);
        }

        return results
            .OrderBy(r => r.DueYear)
            .ThenBy(r => r.DueMonth)
            .ThenBy(r => r.DueDay)
            .ThenBy(r => r.Name);
    }

    public async Task<IEnumerable<BillPaymentOccurrenceResponse>> GetPastAsync()
    {
        var today = DateTime.UtcNow.Date;
        var alerts = (await _alertRepository.GetAllAsync()).ToDictionary(a => a.Id);
        return (await _occurrenceRepository.GetAllAsync())
            .Where(o => o.IsPaid)
            .Select(o => ToResponse(o, alerts.GetValueOrDefault(o.BillPaymentAlertId), today))
            .Where(r => r != null)
            .Cast<BillPaymentOccurrenceResponse>()
            .OrderByDescending(r => r.PaidDate)
            .ThenByDescending(r => r.DueYear)
            .ThenByDescending(r => r.DueMonth);
    }

    public async Task<BillPaymentOccurrenceResponse?> MarkPaidAsync(Guid occurrenceId)
    {
        var occurrence = await _occurrenceRepository.GetAsync(occurrenceId);
        if (occurrence == null) return null;

        occurrence.IsPaid = true;
        occurrence.PaidDate = DateTime.UtcNow;
        occurrence.UpdatedDate = DateTime.UtcNow;
        await _occurrenceRepository.UpsertAsync(occurrence);

        var alert = await _alertRepository.GetAsync(occurrence.BillPaymentAlertId);
        return ToResponse(occurrence, alert, DateTime.UtcNow.Date);
    }

    public async Task<int> GetPendingCountAsync()
    {
        var due = await GetDueThisMonthAsync();
        return due.Count();
    }

    private async Task EnsureOccurrencesAsync()
    {
        var now = DateTime.UtcNow;
        var alerts = (await _alertRepository.GetAllAsync()).Where(a => a.IsActive).ToList();
        if (alerts.Count == 0) return;

        var existing = (await _occurrenceRepository.GetAllAsync()).ToList();
        var existingKeys = existing
            .Select(o => (o.BillPaymentAlertId, o.DueYear, o.DueMonth))
            .ToHashSet();

        var startCursor = new DateTime(now.Year, now.Month, 1).AddMonths(-OverdueBackfillMonths);
        var endCursor = new DateTime(now.Year, now.Month, 1);

        for (var cursor = startCursor; cursor <= endCursor; cursor = cursor.AddMonths(1))
        {
            foreach (var alert in alerts)
            {
                if (!AppliesInMonth(alert, cursor.Year, cursor.Month))
                    continue;
                if (existingKeys.Contains((alert.Id, cursor.Year, cursor.Month)))
                    continue;

                var dueDay = ClampDueDay(alert.DueDay, cursor.Year, cursor.Month);
                var occurrence = new BillPaymentOccurrence
                {
                    Id = Guid.NewGuid(),
                    BillPaymentAlertId = alert.Id,
                    DueYear = cursor.Year,
                    DueMonth = cursor.Month,
                    DueDay = dueDay,
                    IsPaid = false,
                    CreatedDate = DateTime.UtcNow,
                    UpdatedDate = DateTime.UtcNow
                };
                await _occurrenceRepository.UpsertAsync(occurrence);
                existingKeys.Add((alert.Id, cursor.Year, cursor.Month));
            }
        }

        // Keep due day in sync for unpaid current/backfilled occurrences when alert was edited
        foreach (var occurrence in existing.Where(o => !o.IsPaid))
        {
            var alert = alerts.FirstOrDefault(a => a.Id == occurrence.BillPaymentAlertId);
            if (alert == null) continue;
            var expectedDay = ClampDueDay(alert.DueDay, occurrence.DueYear, occurrence.DueMonth);
            if (occurrence.DueDay != expectedDay)
            {
                occurrence.DueDay = expectedDay;
                occurrence.UpdatedDate = DateTime.UtcNow;
                await _occurrenceRepository.UpsertAsync(occurrence);
            }
        }
    }

    internal static bool AppliesInMonth(BillPaymentAlert alert, int year, int month)
    {
        var monthKey = year * 12 + month;
        var startKey = alert.StartYear * 12 + alert.StartMonth;
        if (monthKey < startKey) return false;

        if (alert.EndYear.HasValue && alert.EndMonth.HasValue)
        {
            var endKey = alert.EndYear.Value * 12 + alert.EndMonth.Value;
            if (monthKey > endKey) return false;
        }

        return alert.Recurrence switch
        {
            BillRecurrence.Monthly => true,
            BillRecurrence.Yearly => month == alert.StartMonth,
            BillRecurrence.Once => year == alert.StartYear && month == alert.StartMonth,
            _ => false
        };
    }

    internal static int ClampDueDay(int dueDay, int year, int month)
    {
        var lastDay = DateTime.DaysInMonth(year, month);
        return Math.Clamp(dueDay, 1, lastDay);
    }

    private static BillPaymentOccurrenceResponse? ToResponse(
        BillPaymentOccurrence occurrence,
        BillPaymentAlert? alert,
        DateTime today)
    {
        if (alert == null) return null;

        var due = new DateTime(occurrence.DueYear, occurrence.DueMonth, occurrence.DueDay);
        return new BillPaymentOccurrenceResponse
        {
            Id = occurrence.Id,
            BillPaymentAlertId = occurrence.BillPaymentAlertId,
            Name = alert.Name,
            Notes = alert.Notes,
            Amount = alert.Amount,
            Currency = alert.Currency.ToString(),
            Recurrence = alert.Recurrence.ToString(),
            DueYear = occurrence.DueYear,
            DueMonth = occurrence.DueMonth,
            DueDay = occurrence.DueDay,
            IsPaid = occurrence.IsPaid,
            PaidDate = occurrence.PaidDate,
            IsOverdue = !occurrence.IsPaid && due.Date < today,
            IsVirtual = false
        };
    }

    private static BillPaymentOccurrenceResponse ToVirtualResponse(
        BillPaymentAlert alert,
        int year,
        int month,
        DateTime today)
    {
        var dueDay = ClampDueDay(alert.DueDay, year, month);
        var due = new DateTime(year, month, dueDay);
        return new BillPaymentOccurrenceResponse
        {
            Id = Guid.Empty,
            BillPaymentAlertId = alert.Id,
            Name = alert.Name,
            Notes = alert.Notes,
            Amount = alert.Amount,
            Currency = alert.Currency.ToString(),
            Recurrence = alert.Recurrence.ToString(),
            DueYear = year,
            DueMonth = month,
            DueDay = dueDay,
            IsPaid = false,
            IsOverdue = due.Date < today,
            IsVirtual = true
        };
    }
}
