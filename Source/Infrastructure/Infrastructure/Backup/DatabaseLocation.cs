namespace WealthLedger.Infrastructure.Backup;

/// <summary>Resolved absolute path of the SQLite database file and the folders derived from it.</summary>
public sealed class DatabaseLocation
{
    public DatabaseLocation(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path is required.", nameof(databasePath));

        DatabasePath = Path.GetFullPath(databasePath);
    }

    public string DatabasePath { get; }

    /// <summary>Folder that contains the database file. Backups and settings live here too.</summary>
    public string DataDirectory => Path.GetDirectoryName(DatabasePath)!;

    public string BackupDirectory => Path.Combine(DataDirectory, "Backups");

    public string SettingsPath => Path.Combine(DataDirectory, "backup-settings.json");

    public string AppSettingsPath => Path.Combine(DataDirectory, "app-settings.json");

    /// <summary>Key file used to encrypt secrets on platforms without DPAPI.</summary>
    public string AppSettingsKeyPath => Path.Combine(DataDirectory, "app-settings.key");

    public string PendingRestorePath => Path.Combine(DataDirectory, "restore.pending");
}
