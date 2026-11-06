using WealthLedger.Contracts.Api.Backups;

namespace WealthLedger.Application.Services;

/// <summary>
/// Creates, lists, prunes and restores versioned copies of the SQLite database.
/// Invalid file names or settings are reported with <see cref="ArgumentException"/>;
/// missing backups with <see cref="FileNotFoundException"/>.
/// </summary>
public interface IBackupService
{
    Task<BackupStatus> GetStatusAsync(CancellationToken ct = default);

    Task<BackupSettings> GetSettingsAsync(CancellationToken ct = default);

    Task<BackupSettings> UpdateSettingsAsync(BackupSettings settings, CancellationToken ct = default);

    Task<IReadOnlyList<BackupInfo>> ListAsync(CancellationToken ct = default);

    Task<BackupInfo> CreateAsync(string reason = "manual", CancellationToken ct = default);

    /// <summary>Creates a "scheduled" backup when enabled and the last backup is older than the interval.</summary>
    Task<bool> RunScheduledBackupIfDueAsync(CancellationToken ct = default);

    /// <summary>Creates a "pre-upgrade" backup when the database schema is older than the running app expects.</summary>
    Task<BackupInfo?> CreatePreUpgradeBackupAsync(CancellationToken ct = default);

    Task<string> GetBackupFilePathAsync(string fileName, CancellationToken ct = default);

    Task DeleteAsync(string fileName, CancellationToken ct = default);

    /// <summary>Validates the backup and marks it to be restored on the next application start.</summary>
    Task StageRestoreAsync(string fileName, CancellationToken ct = default);

    /// <summary>Applies a staged restore. Must run before the database is opened. Returns true when a restore was applied.</summary>
    Task<bool> ApplyPendingRestoreAsync(CancellationToken ct = default);
}
