using Microsoft.Data.Sqlite;
using WealthLedger.Infrastructure.Backup;

namespace WealthLedger.Infrastructure.Tests;

/// <summary>A unique folder per test, removed afterwards (SQLite pools are cleared first so handles are released).</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wl-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort on Windows; the OS temp folder is cleaned eventually.
        }
    }
}

/// <summary>Clock that only moves when a test says so.</summary>
internal sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}

/// <summary>Small helpers that talk to a SQLite file directly, without EF.</summary>
internal static class SqliteFile
{
    public static void Execute(string path, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public static object? Scalar(string path, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ConnectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    /// <summary>Creates a database with one marker table holding a single value.</summary>
    public static void CreateMarkerDatabase(string path, string value)
    {
        Execute(path, "DROP TABLE IF EXISTS Marker; CREATE TABLE Marker (Value TEXT NOT NULL); " +
                      $"INSERT INTO Marker (Value) VALUES ('{value}');");
    }

    public static string ReadMarker(string path) => (string)Scalar(path, "SELECT Value FROM Marker LIMIT 1")!;
}

internal static class BackupTestFactory
{
    public static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static BackupService Create(DatabaseLocation location, TimeProvider time) =>
        new(location, time, Microsoft.Extensions.Logging.Abstractions.NullLogger<BackupService>.Instance);
}
