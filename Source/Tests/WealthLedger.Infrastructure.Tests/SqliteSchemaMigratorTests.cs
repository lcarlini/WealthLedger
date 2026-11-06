using Microsoft.EntityFrameworkCore;
using WealthLedger.Infrastructure.Backup;
using WealthLedger.Infrastructure.Data;
using Xunit;

namespace WealthLedger.Infrastructure.Tests;

public sealed class SqliteSchemaMigratorTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly string _path;

    public SqliteSchemaMigratorTests()
    {
        _path = Path.Combine(_dir.Path, "wealthledger.db");
    }

    public void Dispose() => _dir.Dispose();

    private WealthLedgerDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<WealthLedgerDbContext>().UseSqlite($"Data Source={_path}").Options);

    [Fact]
    public async Task ApplyAsync_records_the_schema_version_and_is_safe_to_run_again()
    {
        await using (var db = CreateContext())
        {
            db.Database.EnsureCreated();
            await SqliteSchemaMigrator.ApplyAsync(db);
        }

        Assert.Equal(SqliteSchemaMigrator.CurrentVersion, ReadUserVersion());

        // Second run must be a no-op and must not throw.
        await using var again = CreateContext();
        await SqliteSchemaMigrator.ApplyAsync(again);
        Assert.Equal(SqliteSchemaMigrator.CurrentVersion, ReadUserVersion());
    }

    [Fact]
    public async Task ApplyAsync_upgrades_a_database_created_before_versioning()
    {
        await using (var db = CreateContext())
        {
            db.Database.EnsureCreated();
        }

        // Simulates databases produced by earlier releases, which never set user_version.
        SqliteFile.Execute(_path, "PRAGMA user_version = 0;");
        Assert.Equal(0, ReadUserVersion());

        await using var upgrade = CreateContext();
        await SqliteSchemaMigrator.ApplyAsync(upgrade);

        Assert.Equal(SqliteSchemaMigrator.CurrentVersion, ReadUserVersion());
    }

    private int ReadUserVersion() => Convert.ToInt32(SqliteFile.Scalar(_path, "PRAGMA user_version"));
}
