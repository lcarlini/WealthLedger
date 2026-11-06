namespace WealthLedger.Contracts.Api.Backups;

/// <summary>User-configurable backup policy. Persisted outside the database so it survives DB problems.</summary>
public sealed record BackupSettings(bool Enabled = true, int IntervalDays = 15, int MaxBackups = 10);

/// <summary>One versioned backup file in the Backups folder.</summary>
public sealed record BackupInfo(string FileName, DateTime CreatedUtc, long SizeBytes, string Reason);

/// <summary>Snapshot of backup configuration and state for the Settings page.</summary>
public sealed record BackupStatus(
    BackupSettings Settings,
    string DatabasePath,
    string BackupDirectory,
    int BackupCount,
    DateTime? LastBackupUtc,
    DateTime? NextScheduledBackupUtc,
    bool RestorePending);
