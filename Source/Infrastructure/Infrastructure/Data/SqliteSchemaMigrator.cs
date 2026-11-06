using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace WealthLedger.Infrastructure.Data;

/// <summary>
/// EnsureCreated does not alter existing SQLite databases. This helper adds
/// missing columns/tables so upgrades stay compatible with existing wealthledger.db files.
/// </summary>
public static class SqliteSchemaMigrator
{
    /// <summary>
    /// Schema version stored in PRAGMA user_version. Bump this whenever a step below is added,
    /// so the pre-upgrade backup is taken and the new step runs exactly once per database.
    /// </summary>
    public const int CurrentVersion = 3;

    public static async Task ApplyAsync(WealthLedgerDbContext db, ILogger? logger = null)
    {
        await db.Database.OpenConnectionAsync();
        try
        {
            if (await GetUserVersionAsync(db) >= CurrentVersion)
                return;

            // All steps succeed or none do, so a crash mid-upgrade never leaves a half-migrated schema.
            await using var transaction = await db.Database.BeginTransactionAsync();

            await EnsureInvestmentColumnAsync(db, "Ticker", """ALTER TABLE "Investments" ADD COLUMN "Ticker" TEXT""", logger);
            await EnsureInvestmentColumnAsync(db, "Quantity", """ALTER TABLE "Investments" ADD COLUMN "Quantity" REAL""", logger);
            await EnsureInvestmentColumnAsync(db, "AveragePrice", """ALTER TABLE "Investments" ADD COLUMN "AveragePrice" REAL""", logger);
            await EnsureInvestmentColumnAsync(db, "StartDate", """ALTER TABLE "Investments" ADD COLUMN "StartDate" TEXT""", logger);
            await EnsureInvestmentColumnAsync(db, "PrincipalAmount", """ALTER TABLE "Investments" ADD COLUMN "PrincipalAmount" REAL NOT NULL DEFAULT 0""", logger);
            await EnsureInvestmentColumnAsync(db, "Status", """ALTER TABLE "Investments" ADD COLUMN "Status" INTEGER NOT NULL DEFAULT 1""", logger);
            await EnsureInvestmentColumnAsync(db, "ArchivedAt", """ALTER TABLE "Investments" ADD COLUMN "ArchivedAt" TEXT""", logger);
            await EnsureInvestmentColumnAsync(db, "RedeemedAmount", """ALTER TABLE "Investments" ADD COLUMN "RedeemedAmount" REAL""", logger);
            await EnsureInvestmentColumnAsync(db, "ArchiveNotes", """ALTER TABLE "Investments" ADD COLUMN "ArchiveNotes" TEXT""", logger);
            await EnsureInvestmentColumnAsync(db, "BookAmountAtArchive", """ALTER TABLE "Investments" ADD COLUMN "BookAmountAtArchive" REAL""", logger);
            await EnsureInvestmentColumnAsync(db, "ExpectedTheoreticalAtArchive", """ALTER TABLE "Investments" ADD COLUMN "ExpectedTheoreticalAtArchive" REAL""", logger);
            await EnsureInvestmentColumnAsync(db, "IsIncomeTaxExempt", """ALTER TABLE "Investments" ADD COLUMN "IsIncomeTaxExempt" INTEGER NOT NULL DEFAULT 0""", logger);

            // Backfill lifecycle fields for existing rows (idempotent-ish).
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE "Investments"
                SET "PrincipalAmount" = "Amount"
                WHERE ("PrincipalAmount" IS NULL OR "PrincipalAmount" = 0) AND "Amount" > 0
                """);
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE "Investments"
                SET "StartDate" = "CreatedDate"
                WHERE "StartDate" IS NULL OR TRIM("StartDate") = ''
                """);
            await db.Database.ExecuteSqlRawAsync("""
                UPDATE "Investments"
                SET "Status" = 1
                WHERE "Status" IS NULL OR "Status" = 0
                """);

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

            await EnsureTableAsync(db, "BillPaymentAlerts", """
                CREATE TABLE IF NOT EXISTS "BillPaymentAlerts" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_BillPaymentAlerts" PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "Notes" TEXT NULL,
                    "Amount" REAL NOT NULL,
                    "Currency" INTEGER NOT NULL,
                    "DueDay" INTEGER NOT NULL,
                    "Recurrence" INTEGER NOT NULL,
                    "StartYear" INTEGER NOT NULL,
                    "StartMonth" INTEGER NOT NULL,
                    "EndYear" INTEGER NULL,
                    "EndMonth" INTEGER NULL,
                    "IsActive" INTEGER NOT NULL,
                    "CreatedDate" TEXT NOT NULL,
                    "UpdatedDate" TEXT NOT NULL
                );
                """, logger);

            await EnsureTableAsync(db, "BillPaymentOccurrences", """
                CREATE TABLE IF NOT EXISTS "BillPaymentOccurrences" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_BillPaymentOccurrences" PRIMARY KEY,
                    "BillPaymentAlertId" TEXT NOT NULL,
                    "DueYear" INTEGER NOT NULL,
                    "DueMonth" INTEGER NOT NULL,
                    "DueDay" INTEGER NOT NULL,
                    "IsPaid" INTEGER NOT NULL,
                    "PaidDate" TEXT NULL,
                    "CreatedDate" TEXT NOT NULL,
                    "UpdatedDate" TEXT NOT NULL
                );
                """, logger);

            await EnsureIndexAsync(db, "IX_BillPaymentOccurrences_AlertYearMonth", """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_BillPaymentOccurrences_AlertYearMonth"
                    ON "BillPaymentOccurrences" ("BillPaymentAlertId", "DueYear", "DueMonth");
                """, logger);

            await EnsureIndexAsync(db, "IX_BillPaymentOccurrences_DueYearMonth", """
                CREATE INDEX IF NOT EXISTS "IX_BillPaymentOccurrences_DueYearMonth"
                    ON "BillPaymentOccurrences" ("DueYear", "DueMonth");
                """, logger);

            // Version 2: trade ledger. Decimal columns are TEXT, matching EF's default SQLite mapping.
            await EnsureTableAsync(db, "TradeTransactions", """
                CREATE TABLE IF NOT EXISTS "TradeTransactions" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_TradeTransactions" PRIMARY KEY,
                    "InvestmentId" TEXT NOT NULL,
                    "Side" INTEGER NOT NULL,
                    "TradeDate" TEXT NOT NULL,
                    "Quantity" TEXT NOT NULL,
                    "UnitPrice" TEXT NOT NULL,
                    "Fees" TEXT NOT NULL,
                    "Notes" TEXT NULL,
                    "CreatedDate" TEXT NOT NULL,
                    "UpdatedDate" TEXT NOT NULL
                );
                """, logger);

            await EnsureIndexAsync(db, "IX_TradeTransactions_InvestmentId", """
                CREATE INDEX IF NOT EXISTS "IX_TradeTransactions_InvestmentId"
                    ON "TradeTransactions" ("InvestmentId");
                """, logger);

            // Version 3: monthly budgets per category.
            await EnsureTableAsync(db, "Budgets", """
                CREATE TABLE IF NOT EXISTS "Budgets" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Budgets" PRIMARY KEY,
                    "Category" TEXT NOT NULL COLLATE NOCASE,
                    "MonthlyLimit" TEXT NOT NULL,
                    "CreatedDate" TEXT NOT NULL,
                    "UpdatedDate" TEXT NOT NULL
                );
                """, logger);

            await EnsureIndexAsync(db, "IX_Budgets_Category", """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Budgets_Category" ON "Budgets" ("Category");
                """, logger);

            await db.Database.ExecuteSqlRawAsync($"PRAGMA user_version = {CurrentVersion}");
            await transaction.CommitAsync();
            logger?.LogInformation("Database schema upgraded to version {Version}.", CurrentVersion);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<int> GetUserVersionAsync(WealthLedgerDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version";
        var value = await cmd.ExecuteScalarAsync();
        return value is long version ? (int)version : 0;
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

    private static async Task EnsureIndexAsync(
        WealthLedgerDbContext db,
        string indexName,
        string createSql,
        ILogger? logger)
    {
        if (await IndexExistsAsync(db, indexName))
            return;

        logger?.LogInformation("Creating missing index {Index}", indexName);
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

    private static async Task<bool> IndexExistsAsync(WealthLedgerDbContext db, string indexName)
    {
        var connection = db.Database.GetDbConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name=$name";
        var p = cmd.CreateParameter();
        p.ParameterName = "$name";
        p.Value = indexName;
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
