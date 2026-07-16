using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace WealthLedger.Infrastructure.Data;

/// <summary>
/// EnsureCreated does not alter existing SQLite databases. This helper adds
/// missing columns/tables so upgrades stay compatible with existing wealthledger.db files.
/// </summary>
public static class SqliteSchemaMigrator
{
    public static async Task ApplyAsync(WealthLedgerDbContext db, ILogger? logger = null)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            await EnsureInvestmentColumnAsync(db, "Ticker", """ALTER TABLE "Investments" ADD COLUMN "Ticker" TEXT""", logger);
            await EnsureInvestmentColumnAsync(db, "Quantity", """ALTER TABLE "Investments" ADD COLUMN "Quantity" REAL""", logger);
            await EnsureInvestmentColumnAsync(db, "AveragePrice", """ALTER TABLE "Investments" ADD COLUMN "AveragePrice" REAL""", logger);

            await EnsureTableAsync(db, "PassiveIncomes", """
                CREATE TABLE IF NOT EXISTS "PassiveIncomes" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_PassiveIncomes" PRIMARY KEY,
                    "InvestmentId" TEXT NULL,
                    "Name" TEXT NOT NULL,
                    "IncomeType" INTEGER NOT NULL,
                    "Currency" INTEGER NOT NULL,
                    "Amount" REAL NOT NULL,
                    "PaymentDate" TEXT NOT NULL,
                    "Notes" TEXT NULL,
                    "CreatedDate" TEXT NOT NULL,
                    "UpdatedDate" TEXT NOT NULL
                );
                """, logger);

            await EnsureTableAsync(db, "InvestmentGoals", """
                CREATE TABLE IF NOT EXISTS "InvestmentGoals" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_InvestmentGoals" PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "GoalType" INTEGER NOT NULL,
                    "Currency" INTEGER NOT NULL,
                    "TargetAmount" REAL NOT NULL,
                    "CurrentAmount" REAL NOT NULL,
                    "TargetDate" TEXT NULL,
                    "MonthlyContribution" REAL NULL,
                    "ExpectedAnnualReturnPercent" REAL NULL,
                    "Notes" TEXT NULL,
                    "IsCompleted" INTEGER NOT NULL,
                    "CreatedDate" TEXT NOT NULL,
                    "UpdatedDate" TEXT NOT NULL
                );
                """, logger);

            await EnsureTableAsync(db, "PortfolioSnapshots", """
                CREATE TABLE IF NOT EXISTS "PortfolioSnapshots" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_PortfolioSnapshots" PRIMARY KEY,
                    "SnapshotDate" TEXT NOT NULL,
                    "TotalAmountBrl" REAL NOT NULL,
                    "CashAmountBrl" REAL NOT NULL,
                    "FixedIncomeAmountBrl" REAL NOT NULL,
                    "VariableIncomeAmountBrl" REAL NOT NULL,
                    "UnrealizedGainBrl" REAL NOT NULL,
                    "InvestmentCount" INTEGER NOT NULL,
                    "Notes" TEXT NULL,
                    "CreatedDate" TEXT NOT NULL,
                    "UpdatedDate" TEXT NOT NULL
                );
                """, logger);

            await EnsureTableAsync(db, "WatchlistItems", """
                CREATE TABLE IF NOT EXISTS "WatchlistItems" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_WatchlistItems" PRIMARY KEY,
                    "Ticker" TEXT NOT NULL,
                    "Name" TEXT NOT NULL,
                    "AccountType" INTEGER NOT NULL,
                    "TargetPrice" REAL NULL,
                    "AlertAbove" REAL NULL,
                    "AlertBelow" REAL NULL,
                    "Notes" TEXT NULL,
                    "CreatedDate" TEXT NOT NULL,
                    "UpdatedDate" TEXT NOT NULL
                );
                """, logger);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureInvestmentColumnAsync(
        WealthLedgerDbContext db,
        string column,
        string alterSql,
        ILogger? logger)
    {
        if (await ColumnExistsAsync(db, "Investments", column))
            return;

        logger?.LogInformation("Adding missing column Investments.{Column}", column);
        await db.Database.ExecuteSqlRawAsync(alterSql);
    }

    private static async Task EnsureTableAsync(
        WealthLedgerDbContext db,
        string table,
        string createSql,
        ILogger? logger)
    {
        if (await TableExistsAsync(db, table))
            return;

        logger?.LogInformation("Creating missing table {Table}", table);
        await db.Database.ExecuteSqlRawAsync(createSql);
    }

    private static async Task<bool> TableExistsAsync(WealthLedgerDbContext db, string table)
    {
        var connection = db.Database.GetDbConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$name";
        var p = cmd.CreateParameter();
        p.ParameterName = "$name";
        p.Value = table;
        cmd.Parameters.Add(p);
        var result = await cmd.ExecuteScalarAsync();
        return result != null && result != DBNull.Value;
    }

    private static async Task<bool> ColumnExistsAsync(WealthLedgerDbContext db, string table, string column)
    {
        var connection = db.Database.GetDbConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(1);
            if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
