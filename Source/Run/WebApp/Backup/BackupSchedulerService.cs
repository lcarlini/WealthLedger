using WealthLedger.Application.Services;

namespace WealthLedger.WebApp.Backup;

/// <summary>
/// Checks once at startup and then hourly whether a scheduled backup is due.
/// The actual "is it due" decision (interval, enabled flag) lives in <see cref="IBackupService"/>.
/// </summary>
public sealed class BackupSchedulerService(IBackupService backups, ILogger<BackupSchedulerService> logger)
    : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        await RunCheckAsync(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCheckAsync(stoppingToken);
        }
    }

    private async Task RunCheckAsync(CancellationToken ct)
    {
        try
        {
            await backups.RunScheduledBackupIfDueAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Host is shutting down.
        }
        catch (Exception ex)
        {
            // A failed backup must never take the application down; it is retried on the next tick.
            logger.LogError(ex, "Scheduled database backup failed; it will be retried in {Hours} hour(s).", CheckInterval.TotalHours);
        }
    }
}
