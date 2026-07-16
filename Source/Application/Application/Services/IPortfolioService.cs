using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;

namespace WealthLedger.Application.Services;

public interface IPortfolioService
{
    Task<PortfolioOverviewResponse> GetOverviewAsync(int projectionYears = 3, CancellationToken cancellationToken = default);

    Task<PortfolioSnapshot> CaptureSnapshotAsync(string? notes = null, CancellationToken cancellationToken = default);

    Task<IEnumerable<PortfolioSnapshot>> GetSnapshotsAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<CalendarEventItem>> GetCalendarAsync(int monthsAhead = 12, CancellationToken cancellationToken = default);
}

public interface IPassiveIncomeService
{
    Task<IEnumerable<PassiveIncome>> GetAllAsync();
    Task<PassiveIncome?> GetAsync(Guid id);
    Task<PassiveIncome> UpsertAsync(PassiveIncome entity);
    Task DeleteAsync(Guid id);
}

public interface IInvestmentGoalService
{
    Task<IEnumerable<InvestmentGoal>> GetAllAsync();
    Task<InvestmentGoal?> GetAsync(Guid id);
    Task<InvestmentGoal> UpsertAsync(InvestmentGoal entity);
    Task DeleteAsync(Guid id);
}

public interface IWatchlistService
{
    Task<IEnumerable<WatchlistItem>> GetAllAsync();
    Task<WatchlistItem?> GetAsync(Guid id);
    Task<WatchlistItem> UpsertAsync(WatchlistItem entity);
    Task DeleteAsync(Guid id);
}

public interface IDataExportService
{
    Task<object> ExportJsonAsync(CancellationToken cancellationToken = default);
    Task ImportJsonAsync(Stream jsonStream, CancellationToken cancellationToken = default);
}
