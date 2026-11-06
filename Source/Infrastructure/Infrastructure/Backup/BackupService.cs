using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Backups;
using WealthLedger.Infrastructure.Data;

namespace WealthLedger.Infrastructure.Backup;

public sealed partial class BackupService : IBackupService, IDisposable
{
    public const int MinIntervalDays = 1;
    public const int MaxIntervalDays = 365;
    public const int MinMaxBackups = 1;
    public const int MaxMaxBackups = 100;

    public const string ReasonManual = "manual";
    public const string ReasonScheduled = "scheduled";
    public const string ReasonPreUpgrade = "pre-upgrade";
    public const string ReasonPreRestore = "pre-restore";
    public const string ReasonPreImport = "pre-import";

    private static readonly string[] KnownReasons = [ReasonManual, ReasonScheduled, ReasonPreUpgrade, ReasonPreRestore, ReasonPreImport];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    // Only files produced by this service are visible or addressable: wealthledger-yyyyMMdd-HHmmss-<reason>[-n].db
    [GeneratedRegex(@"^wealthledger-(?<stamp>\d{8}-\d{6})-(?<reason>manual|scheduled|pre-upgrade|pre-restore|pre-import)(?:-\d+)?\.db$")]
    private static partial Regex BackupFileNamePattern();

    private readonly DatabaseLocation _location;
    private readonly TimeProvider _time;
    private readonly ILogger<BackupService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public BackupService(DatabaseLocation location, TimeProvider time, ILogger<BackupService> logger)
    {
        _location = location;
        _time = time;
        _logger = logger;
    }

    public async Task<BackupStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var settings = await GetSettingsAsync(ct);
        var backups = await ListAsync(ct);
        var latest = backups.FirstOrDefault();
        var now = _time.GetUtcNow().UtcDateTime;

        return new BackupStatus(
            Settings: settings,
            DatabasePath: _location.DatabasePath,
            BackupDirectory: _location.BackupDirectory,
            BackupCount: backups.Count,
            LastBackupUtc: latest?.CreatedUtc,
            NextScheduledBackupUtc: !settings.Enabled ? null
                : latest is null ? now
                : latest.CreatedUtc.AddDays(settings.IntervalDays),
            RestorePending: File.Exists(_location.PendingRestorePath));
    }

    public async Task<BackupSettings> GetSettingsAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_location.SettingsPath))
            return new BackupSettings();

        try
        {
            await using var stream = File.OpenRead(_location.SettingsPath);
            var loaded = await JsonSerializer.DeserializeAsync<BackupSettings>(stream, JsonOptions, ct);
            return loaded is null ? new BackupSettings() : Validate(loaded);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or IOException)
        {
            // A damaged settings file must never block backups; fall back to defaults and say so.
            _logger.LogWarning(ex, "Backup settings file is unreadable; using defaults.");
            return new BackupSettings();
        }
    }

    public async Task<BackupSettings> UpdateSettingsAsync(BackupSettings settings, CancellationToken ct = default)
    {
        var validated = Validate(settings);
        await _gate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(_location.DataDirectory);
            var tmp = _location.SettingsPath + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(validated, JsonOptions), ct);
            File.Move(tmp, _location.SettingsPath, overwrite: true);
            return validated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<IReadOnlyList<BackupInfo>> ListAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<BackupInfo> list = ListCore();
        return Task.FromResult(list);
    }

    public async Task<BackupInfo> CreateAsync(string reason = ReasonManual, CancellationToken ct = default)
    {
        ValidateReason(reason);
        await _gate.WaitAsync(ct);
        try
        {
            return await CreateCoreAsync(reason, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RunScheduledBackupIfDueAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var settings = await GetSettingsAsync(ct);
            if (!settings.Enabled || !File.Exists(_location.DatabasePath))
                return false;

            var latest = ListCore().FirstOrDefault();
            var now = _time.GetUtcNow().UtcDateTime;
            var due = latest is null || now - latest.CreatedUtc >= TimeSpan.FromDays(settings.IntervalDays);
            if (!due)
                return false;

            await CreateCoreAsync(ReasonScheduled, ct);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<BackupInfo?> CreatePreUpgradeBackupAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(_location.DatabasePath))
                return null; // fresh install: nothing to protect yet

            var version = ReadUserVersion(_location.DatabasePath);
            if (version >= SqliteSchemaMigrator.CurrentVersion)
                return null;

            _logger.LogInformation("Schema version {Current} is older than {Expected}; creating a pre-upgrade backup.",
                version, SqliteSchemaMigrator.CurrentVersion);
            return await CreateCoreAsync(ReasonPreUpgrade, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<string> GetBackupFilePathAsync(string fileName, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(ResolveBackupPath(fileName));
    }

    public async Task DeleteAsync(string fileName, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var path = ResolveBackupPath(fileName);
            File.Delete(path);
            _logger.LogInformation("Deleted backup {FileName}.", fileName);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StageRestoreAsync(string fileName, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var path = ResolveBackupPath(fileName);
            if (!IsHealthy(path))
                throw new InvalidOperationException($"Backup '{fileName}' failed the integrity check and cannot be restored.");

            Directory.CreateDirectory(_location.DataDirectory);
            var tmp = _location.PendingRestorePath + ".tmp";
            await File.WriteAllTextAsync(tmp, fileName, ct);
            File.Move(tmp, _location.PendingRestorePath, overwrite: true);
            _logger.LogWarning("Restore of {FileName} staged. It will be applied on the next application start.", fileName);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> ApplyPendingRestoreAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_location.PendingRestorePath))
            return false;

        await _gate.WaitAsync(ct);
        try
        {
            var fileName = (await File.ReadAllTextAsync(_location.PendingRestorePath, ct)).Trim();
            string source;
            try
            {
                source = ResolveBackupPath(fileName);
                if (!IsHealthy(source))
                    throw new InvalidOperationException("integrity check failed");
            }
            catch (Exception ex) when (ex is ArgumentException or FileNotFoundException or InvalidOperationException)
            {
                // Keep the current database; drop the bad marker so startup is never blocked by it.
                _logger.LogError(ex, "Staged restore '{FileName}' cannot be applied; the current database was kept.", fileName);
                File.Delete(_location.PendingRestorePath);
                return false;
            }

            // Safety net: snapshot the current database first. If this fails, the restore is aborted.
            if (File.Exists(_location.DatabasePath))
                await CreateCoreAsync(ReasonPreRestore, ct);

            SqliteConnection.ClearAllPools();
            var staging = _location.DatabasePath + ".restoring";
            File.Copy(source, staging, overwrite: true);
            DeleteIfExists(_location.DatabasePath + "-wal");
            DeleteIfExists(_location.DatabasePath + "-shm");
            File.Move(staging, _location.DatabasePath, overwrite: true);
            File.Delete(_location.PendingRestorePath);

            _logger.LogWarning("Database restored from backup {FileName}.", fileName);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    // ---- internals (callers must hold _gate) -------------------------------------------------

    private async Task<BackupInfo> CreateCoreAsync(string reason, CancellationToken ct)
    {
        if (!File.Exists(_location.DatabasePath))
            throw new InvalidOperationException("The database file does not exist yet; there is nothing to back up.");

        Directory.CreateDirectory(_location.BackupDirectory);
        var now = _time.GetUtcNow().UtcDateTime;
        var target = NextFreePath(now, reason);
        var tmp = target + ".tmp";

        try
        {
            // VACUUM INTO produces a consistent snapshot even while the app is using the database.
            await using (var source = OpenReadOnly(_location.DatabasePath))
            {
                await source.OpenAsync(ct);
                await using var cmd = source.CreateCommand();
                cmd.CommandText = "VACUUM INTO $target";
                cmd.Parameters.AddWithValue("$target", tmp);
                await cmd.ExecuteNonQueryAsync(ct);
            }

            if (!IsHealthy(tmp))
                throw new InvalidOperationException("The new backup failed the integrity check and was discarded.");

            SqliteConnection.ClearAllPools(); // release file handles before renaming
            File.Move(tmp, target);
        }
        catch
        {
            DeleteIfExists(tmp);
            throw;
        }

        var info = ToInfo(target);
        _logger.LogInformation("Created {Reason} backup {FileName} ({Size} bytes).", reason, info.FileName, info.SizeBytes);
        Prune();
        return info;
    }

    private void Prune()
    {
        var max = Validate(GetSettingsCore()).MaxBackups;
        foreach (var old in ListCore().Skip(max))
        {
            try
            {
                File.Delete(Path.Combine(_location.BackupDirectory, old.FileName));
                _logger.LogInformation("Pruned old backup {FileName} (keeping {Max}).", old.FileName, max);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Could not prune backup {FileName}.", old.FileName);
            }
        }
    }

    private BackupSettings GetSettingsCore()
    {
        if (!File.Exists(_location.SettingsPath))
            return new BackupSettings();
        try
        {
            var loaded = JsonSerializer.Deserialize<BackupSettings>(File.ReadAllText(_location.SettingsPath), JsonOptions);
            return loaded is null ? new BackupSettings() : Validate(loaded);
        }
        catch (Exception ex) when (ex is JsonException or IOException or ArgumentException)
        {
            return new BackupSettings();
        }
    }

    private List<BackupInfo> ListCore()
    {
        if (!Directory.Exists(_location.BackupDirectory))
            return [];

        return Directory.EnumerateFiles(_location.BackupDirectory)
            .Select(Path.GetFileName)
            .Where(name => name is not null && BackupFileNamePattern().IsMatch(name))
            .Select(name => ToInfo(Path.Combine(_location.BackupDirectory, name!)))
            .OrderByDescending(b => b.CreatedUtc)
            .ThenByDescending(b => b.FileName, StringComparer.Ordinal)
            .ToList();
    }

    private string NextFreePath(DateTime utc, string reason)
    {
        var stamp = utc.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var candidate = Path.Combine(_location.BackupDirectory, $"wealthledger-{stamp}-{reason}.db");
        for (var n = 2; File.Exists(candidate); n++)
            candidate = Path.Combine(_location.BackupDirectory, $"wealthledger-{stamp}-{reason}-{n}.db");
        return candidate;
    }

    private string ResolveBackupPath(string fileName)
    {
        if (string.IsNullOrEmpty(fileName) || !BackupFileNamePattern().IsMatch(fileName))
            throw new ArgumentException("Invalid backup file name.", nameof(fileName));

        var path = Path.Combine(_location.BackupDirectory, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException("Backup not found.", fileName);

        return path;
    }

    private BackupInfo ToInfo(string path)
    {
        var fileName = Path.GetFileName(path);
        var match = BackupFileNamePattern().Match(fileName);
        var created = DateTime.ParseExact(match.Groups["stamp"].Value, "yyyyMMdd-HHmmss",
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
        return new BackupInfo(fileName, created, new FileInfo(path).Length, match.Groups["reason"].Value);
    }

    private static int ReadUserVersion(string databasePath)
    {
        using var connection = OpenReadOnly(databasePath);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version";
        var value = cmd.ExecuteScalar();
        SqliteConnection.ClearAllPools();
        return value is long l ? (int)l : 0;
    }

    private static bool IsHealthy(string path)
    {
        try
        {
            using var connection = OpenReadOnly(path);
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check";
            var result = cmd.ExecuteScalar() as string;
            return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch (SqliteException)
        {
            return false;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }
    }

    private static SqliteConnection OpenReadOnly(string path)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        };
        return new SqliteConnection(builder.ConnectionString);
    }

    private static void ValidateReason(string reason)
    {
        if (!KnownReasons.Contains(reason))
            throw new ArgumentException($"Unknown backup reason '{reason}'.", nameof(reason));
    }

    public static BackupSettings Validate(BackupSettings settings)
    {
        if (settings.IntervalDays is < MinIntervalDays or > MaxIntervalDays)
            throw new ArgumentException($"Interval must be between {MinIntervalDays} and {MaxIntervalDays} days.", nameof(settings));
        if (settings.MaxBackups is < MinMaxBackups or > MaxMaxBackups)
            throw new ArgumentException($"Maximum backups must be between {MinMaxBackups} and {MaxMaxBackups}.", nameof(settings));
        return settings;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    public void Dispose() => _gate.Dispose();
}
