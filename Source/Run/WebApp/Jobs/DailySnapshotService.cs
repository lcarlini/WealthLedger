using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Domain;

namespace WealthLedger.WebApp.Jobs;

/// <summary>
/// Records one net-worth snapshot per UTC day, so the portfolio timeline has no gaps when the user
/// forgets to capture one. It checks hourly, does nothing when today's snapshot exists, and skips
/// portfolios with no investments so an empty database does not produce zero-value history.
/// </summary>
public sealed class DailySnapshotService(IServiceScopeFactory scopeFactory, ILogger<DailySnapshotService> logger)
    : BackgroundService
{
    public const string AutoNote = "auto";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        await RunOnceAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunOnceAsync(stoppingToken);
        }
    }

    internal async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            // The job is a singleton, but repositories are scoped, so each run gets its own scope.
            using var scope = scopeFactory.CreateScope();
            var investments = scope.ServiceProvider.GetRequiredService<IRepository<Investment>>();
            if (!(await investments.GetAllAsync()).Any())
                return;

            var portfolio = scope.ServiceProvider.GetRequiredService<IPortfolioService>();
            var today = DateTime.UtcNow.Date;
            var snapshots = await portfolio.GetSnapshotsAsync(ct);
            if (snapshots.Any(s => s.SnapshotDate.Date == today))
                return;

            await portfolio.CaptureSnapshotAsync(AutoNote, ct);
            logger.LogInformation("Recorded the daily portfolio snapshot for {Date}.", today.ToString("yyyy-MM-dd"));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutting down.
        }
        catch (Exception ex)
        {
            // Market data outages must not stop the host; the next hourly check retries.
            logger.LogError(ex, "Daily portfolio snapshot failed; it will be retried in an hour.");
        }
    }
}
