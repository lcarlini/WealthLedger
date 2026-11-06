using Microsoft.Data.Sqlite;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Backups;
using WealthLedger.Infrastructure.Backup;
using WealthLedger.Infrastructure.Data;
using Xunit;

namespace WealthLedger.Infrastructure.Tests;

public sealed class BackupServiceTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly DatabaseLocation _location;
    private readonly FakeTimeProvider _time = new(BackupTestFactory.Start);
    private readonly BackupService _service;

    public BackupServiceTests()
    {
        _location = new DatabaseLocation(Path.Combine(_dir.Path, "wealthledger.db"));
        SqliteFile.CreateMarkerDatabase(_location.DatabasePath, "v1");
        _service = BackupTestFactory.Create(_location, _time);
    }

    public void Dispose()
    {
        _service.Dispose();
        _dir.Dispose();
    }

    [Fact]
    public async Task CreateAsync_writes_a_consistent_copy_of_the_database()
    {
        var info = await _service.CreateAsync(BackupService.ReasonManual);

        var copy = Path.Combine(_location.BackupDirectory, info.FileName);
        Assert.True(File.Exists(copy));
        Assert.Equal("v1", SqliteFile.ReadMarker(copy));
        Assert.Equal(BackupService.ReasonManual, info.Reason);
        Assert.Equal(BackupTestFactory.Start.UtcDateTime, info.CreatedUtc);
    }

    [Fact]
    public async Task CreateAsync_rejects_unknown_reasons()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync("whenever"));
    }

    [Fact]
    public async Task Retention_keeps_only_the_newest_configured_backups()
    {
        await _service.UpdateSettingsAsync(new BackupSettings(MaxBackups: 10));

        // 12 backups, one per minute: the two oldest must be pruned.
        for (var i = 1; i <= 12; i++)
        {
            _time.Advance(TimeSpan.FromMinutes(1));
            await _service.CreateAsync(BackupService.ReasonManual);
        }

        var remaining = await _service.ListAsync();
        Assert.Equal(10, remaining.Count);
        Assert.Equal(BackupTestFactory.Start.AddMinutes(12).UtcDateTime, remaining[0].CreatedUtc);
        Assert.Equal(BackupTestFactory.Start.AddMinutes(3).UtcDateTime, remaining[^1].CreatedUtc);
    }

    [Fact]
    public async Task Scheduled_backup_runs_when_none_exist_and_then_every_interval()
    {
        Assert.True(await _service.RunScheduledBackupIfDueAsync());
        Assert.False(await _service.RunScheduledBackupIfDueAsync());

        _time.Advance(TimeSpan.FromDays(14));
        Assert.False(await _service.RunScheduledBackupIfDueAsync());

        _time.Advance(TimeSpan.FromDays(1));
        Assert.True(await _service.RunScheduledBackupIfDueAsync());

        var backups = await _service.ListAsync();
        Assert.Equal(2, backups.Count);
        Assert.All(backups, b => Assert.Equal(BackupService.ReasonScheduled, b.Reason));
    }

    [Fact]
    public async Task Scheduled_backup_does_nothing_when_disabled()
    {
        await _service.UpdateSettingsAsync(new BackupSettings(Enabled: false));

        Assert.False(await _service.RunScheduledBackupIfDueAsync());
        Assert.Empty(await _service.ListAsync());
    }

    [Fact]
    public async Task Status_reports_next_scheduled_backup_from_the_last_one()
    {
        await _service.CreateAsync(BackupService.ReasonManual);

        var status = await _service.GetStatusAsync();

        Assert.Equal(1, status.BackupCount);
        Assert.Equal(BackupTestFactory.Start.AddDays(15).UtcDateTime, status.NextScheduledBackupUtc);
        Assert.False(status.RestorePending);
    }

    [Fact]
    public async Task Pre_upgrade_backup_is_taken_only_when_the_schema_is_behind()
    {
        // Fresh database has user_version 0, which is behind the running schema.
        var first = await _service.CreatePreUpgradeBackupAsync();
        Assert.NotNull(first);
        Assert.Equal(BackupService.ReasonPreUpgrade, first!.Reason);

        SqliteFile.Execute(_location.DatabasePath, $"PRAGMA user_version = {SqliteSchemaMigrator.CurrentVersion}");

        Assert.Null(await _service.CreatePreUpgradeBackupAsync());
    }

    [Fact]
    public async Task Pre_upgrade_backup_is_skipped_when_there_is_no_database_yet()
    {
        File.Delete(_location.DatabasePath);

        Assert.Null(await _service.CreatePreUpgradeBackupAsync());
    }

    [Fact]
    public async Task Restore_round_trip_brings_back_the_backed_up_data_and_keeps_a_safety_copy()
    {
        var backup = await _service.CreateAsync(BackupService.ReasonManual);
        SqliteFile.Execute(_location.DatabasePath, "UPDATE Marker SET Value = 'v2';");

        await _service.StageRestoreAsync(backup.FileName);
        Assert.True((await _service.GetStatusAsync()).RestorePending);
        // Staging alone must not touch the live database.
        Assert.Equal("v2", SqliteFile.ReadMarker(_location.DatabasePath));

        Assert.True(await _service.ApplyPendingRestoreAsync());

        Assert.Equal("v1", SqliteFile.ReadMarker(_location.DatabasePath));
        Assert.False(File.Exists(_location.PendingRestorePath));
        var backups = await _service.ListAsync();
        Assert.Contains(backups, b => b.Reason == BackupService.ReasonPreRestore);
    }

    [Fact]
    public async Task Restore_refuses_a_corrupt_backup_and_leaves_no_pending_marker()
    {
        var corrupt = Path.Combine(_location.BackupDirectory, "wealthledger-20260101-000000-manual.db");
        Directory.CreateDirectory(_location.BackupDirectory);
        File.WriteAllText(corrupt, "this is not a sqlite database");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.StageRestoreAsync("wealthledger-20260101-000000-manual.db"));

        Assert.False(File.Exists(_location.PendingRestorePath));
    }

    [Fact]
    public async Task Pending_restore_for_a_missing_backup_is_dropped_and_the_database_is_kept()
    {
        var backup = await _service.CreateAsync(BackupService.ReasonManual);
        await _service.StageRestoreAsync(backup.FileName);
        File.Delete(Path.Combine(_location.BackupDirectory, backup.FileName));

        Assert.False(await _service.ApplyPendingRestoreAsync());

        Assert.False(File.Exists(_location.PendingRestorePath));
        Assert.Equal("v1", SqliteFile.ReadMarker(_location.DatabasePath));
    }

    [Theory]
    [InlineData("../wealthledger.db")]
    [InlineData("..\\secret.db")]
    [InlineData("wealthledger-20260101-000000-manual.db.bak")]
    [InlineData("notes.txt")]
    [InlineData("")]
    public async Task Backup_names_outside_the_service_pattern_are_rejected(string fileName)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.GetBackupFilePathAsync(fileName));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.DeleteAsync(fileName));
    }

    [Fact]
    public async Task Well_formed_but_missing_backup_names_are_not_found()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            _service.GetBackupFilePathAsync("wealthledger-20250101-000000-manual.db"));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(366, 10)]
    [InlineData(15, 0)]
    [InlineData(15, 101)]
    public async Task Settings_outside_the_allowed_ranges_are_rejected(int intervalDays, int maxBackups)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.UpdateSettingsAsync(new BackupSettings(IntervalDays: intervalDays, MaxBackups: maxBackups)));
    }

    [Fact]
    public async Task Settings_round_trip_through_the_settings_file()
    {
        await _service.UpdateSettingsAsync(new BackupSettings(Enabled: false, IntervalDays: 30, MaxBackups: 4));

        var reloaded = BackupTestFactory.Create(_location, _time);
        var settings = await reloaded.GetSettingsAsync();
        reloaded.Dispose();

        Assert.Equal(new BackupSettings(Enabled: false, IntervalDays: 30, MaxBackups: 4), settings);
    }

    [Fact]
    public async Task A_damaged_settings_file_falls_back_to_defaults()
    {
        File.WriteAllText(_location.SettingsPath, "{ not json");

        Assert.Equal(new BackupSettings(), await _service.GetSettingsAsync());
    }
}
