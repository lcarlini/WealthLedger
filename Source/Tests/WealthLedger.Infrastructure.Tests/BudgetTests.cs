using Microsoft.EntityFrameworkCore;
using WealthLedger.Contracts.Api.Budgets;
using WealthLedger.Contracts.Domain;
using WealthLedger.Infrastructure.Backup;
using WealthLedger.Infrastructure.Data;
using WealthLedger.Infrastructure.Repositories;
using WealthLedger.Infrastructure.Services;
using Xunit;

namespace WealthLedger.Infrastructure.Tests;

public sealed class BudgetTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private static BankTransaction Spend(string? category, decimal amount, int month = 11) => new()
    {
        Amount = -amount, Category = category, Date = new DateTime(2026, month, 10), Description = "x",
    };

    [Theory]
    [InlineData(500, BudgetLevels.Ok, 50)]
    [InlineData(800, BudgetLevels.Warning, 80)]
    [InlineData(1000, BudgetLevels.Warning, 100)]
    [InlineData(1200, BudgetLevels.Over, 120)]
    public void Level_follows_the_share_of_the_limit_used(double spent, string level, double percent)
    {
        var budget = new Budget { Id = Guid.NewGuid(), Category = "Food", MonthlyLimit = 1000m };

        var status = BudgetService.Evaluate([budget], [Spend("Food", (decimal)spent)], 2026, 11);

        var item = Assert.Single(status.Items);
        Assert.Equal(level, item.Level);
        Assert.Equal((decimal)percent, item.PercentUsed);
        Assert.Equal(1000m - (decimal)spent, item.Remaining);
    }

    [Fact]
    public void Spending_without_a_budget_is_reported_largest_first_and_category_match_ignores_case()
    {
        var budget = new Budget { Id = Guid.NewGuid(), Category = "food", MonthlyLimit = 500m };
        var spending = new[]
        {
            Spend("Food", 100m), Spend("FOOD", 50m),
            Spend("Transport", 80m), Spend(null, 300m), // uncategorized counts as "Other"
        };

        var status = BudgetService.Evaluate([budget], spending, 2026, 11);

        Assert.Equal(150m, status.Items.Single().Spent);
        Assert.Equal(["Other", "Transport"], status.Unbudgeted.Select(u => u.Category));
        Assert.Equal(530m, status.TotalSpent);
        Assert.Equal(500m, status.TotalLimit);
    }

    private BudgetService CreateService(out WealthLedgerDbContext db)
    {
        db = new WealthLedgerDbContext(new DbContextOptionsBuilder<WealthLedgerDbContext>()
            .UseSqlite($"Data Source={Path.Combine(_dir.Path, "b.db")}").Options);
        db.Database.EnsureCreated();
        return new BudgetService(new EfRepository<Budget>(db), new EfRepository<BankTransaction>(db));
    }

    [Fact]
    public async Task Saving_the_same_category_again_updates_the_limit_instead_of_duplicating()
    {
        var service = CreateService(out var db);
        await using var _ = db;

        await service.SaveAsync(new BudgetRequest("Food", 400m));
        await service.SaveAsync(new BudgetRequest("  FOOD ", 650.555m));

        var all = await service.GetAllAsync();
        var only = Assert.Single(all);
        Assert.Equal(650.56m, only.MonthlyLimit);
    }

    [Theory]
    [InlineData("", 100)]
    [InlineData("   ", 100)]
    [InlineData("Food", 0)]
    [InlineData("Food", -5)]
    public async Task Invalid_budgets_are_rejected(string category, double limit)
    {
        var service = CreateService(out var db);
        await using var _ = db;

        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(new BudgetRequest(category, (decimal)limit)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public async Task Status_rejects_an_invalid_month(int month)
    {
        var service = CreateService(out var db);
        await using var _ = db;

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetStatusAsync(2026, month));
    }

    [Fact]
    public async Task Upgrading_an_older_database_creates_the_budgets_table()
    {
        var path = Path.Combine(_dir.Path, "old.db");
        var options = new DbContextOptionsBuilder<WealthLedgerDbContext>().UseSqlite($"Data Source={path}").Options;
        await using (var create = new WealthLedgerDbContext(options))
            create.Database.EnsureCreated();

        SqliteFile.Execute(path, "DROP TABLE \"Budgets\"; PRAGMA user_version = 2;");

        await using var upgrade = new WealthLedgerDbContext(options);
        await SqliteSchemaMigrator.ApplyAsync(upgrade);

        Assert.Equal(1L, Convert.ToInt64(SqliteFile.Scalar(path, "SELECT COUNT(*) FROM sqlite_master WHERE name = 'Budgets'")));
        Assert.Equal(SqliteSchemaMigrator.CurrentVersion, Convert.ToInt32(SqliteFile.Scalar(path, "PRAGMA user_version")));
    }
}
