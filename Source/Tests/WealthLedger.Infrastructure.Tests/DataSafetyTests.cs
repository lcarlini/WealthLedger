using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WealthLedger.Contracts.Domain;
using WealthLedger.Infrastructure.Data;
using WealthLedger.Infrastructure.Repositories;
using WealthLedger.Infrastructure.Services;
using Xunit;

namespace WealthLedger.Infrastructure.Tests;

public sealed class DataSafetyTests : IDisposable
{
    private readonly TempDirectory _dir = new();

    public void Dispose() => _dir.Dispose();

    private WealthLedgerDbContext OpenDatabase(string name)
    {
        var path = Path.Combine(_dir.Path, name);
        var db = new WealthLedgerDbContext(
            new DbContextOptionsBuilder<WealthLedgerDbContext>().UseSqlite($"Data Source={path}").Options);
        db.Database.EnsureCreated();
        return db;
    }

    private static DataExportService CreateExport(WealthLedgerDbContext db) => new(
        new EfRepository<FinancialInstitution>(db),
        new EfRepository<Investment>(db),
        new EfRepository<TaskItem>(db),
        new EfRepository<StatementImport>(db),
        new EfRepository<BankTransaction>(db),
        new EfRepository<CashFlowScheduleItem>(db),
        new EfRepository<IncomeProfile>(db),
        new EfRepository<ExtraIncome>(db),
        new EfRepository<BusinessDayOverride>(db),
        new EfRepository<PassiveIncome>(db),
        new EfRepository<InvestmentGoal>(db),
        new EfRepository<PortfolioSnapshot>(db),
        new EfRepository<WatchlistItem>(db),
        new EfRepository<BillPaymentAlert>(db),
        new EfRepository<BillPaymentOccurrence>(db),
        new EfRepository<TradeTransaction>(db),
        new UnitOfWork(db),
        NullLogger<DataExportService>.Instance);

    private static readonly JsonSerializerOptions WireOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private static Stream AsStream(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

    [Fact]
    public async Task Export_then_import_restores_bills_and_income_profile()
    {
        var alertId = Guid.NewGuid();
        await using (var source = OpenDatabase("source.db"))
        {
            var now = DateTime.UtcNow;
            await new EfRepository<FinancialInstitution>(source).UpsertAsync(
                new FinancialInstitution { Id = Guid.NewGuid(), Name = "Bank A", CreatedDate = now, UpdatedDate = now });
            await new EfRepository<BillPaymentAlert>(source).UpsertAsync(new BillPaymentAlert
            {
                Id = alertId, Name = "Electricity", Amount = 123.45m, DueDay = 10, StartYear = 2026, StartMonth = 1,
                CreatedDate = now, UpdatedDate = now,
            });
            await new EfRepository<BillPaymentOccurrence>(source).UpsertAsync(new BillPaymentOccurrence
            {
                Id = Guid.NewGuid(), BillPaymentAlertId = alertId, DueYear = 2026, DueMonth = 1, DueDay = 10,
                CreatedDate = now, UpdatedDate = now,
            });
            await new EfRepository<IncomeProfile>(source).UpsertAsync(new IncomeProfile
            {
                Id = Guid.NewGuid(), HourlyRateUsd = 50, HoursPerDay = 6, UsdBrlRate = 5.5m, TaxPercent = 27.5m,
                CreatedDate = now, UpdatedDate = now,
            });

            var payload = await CreateExport(source).ExportJsonAsync();
            var json = JsonSerializer.Serialize(payload, WireOptions);

            await using var target = OpenDatabase("target.db");
            await CreateExport(target).ImportJsonAsync(AsStream(json));

            Assert.Single(await new EfRepository<FinancialInstitution>(target).GetAllAsync());
            var bills = (await new EfRepository<BillPaymentAlert>(target).GetAllAsync()).ToList();
            Assert.Single(bills);
            Assert.Equal(123.45m, bills[0].Amount);
            Assert.Single(await new EfRepository<BillPaymentOccurrence>(target).GetAllAsync());
            var profile = (await new EfRepository<IncomeProfile>(target).GetAllAsync()).Single();
            Assert.Equal(27.5m, profile.TaxPercent);
        }
    }

    [Fact]
    public async Task Import_rejects_a_file_from_a_newer_format_without_changing_anything()
    {
        await using var db = OpenDatabase("newer.db");
        await new EfRepository<FinancialInstitution>(db).UpsertAsync(
            new FinancialInstitution { Id = Guid.NewGuid(), Name = "Keep me", CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow });

        var json = """{ "formatVersion": 99, "financialInstitutions": [] }""";

        await Assert.ThrowsAsync<InvalidDataException>(() => CreateExport(db).ImportJsonAsync(AsStream(json)));
        Assert.Single(await new EfRepository<FinancialInstitution>(db).GetAllAsync());
    }

    [Fact]
    public async Task Import_is_all_or_nothing_when_a_later_section_is_invalid()
    {
        await using var db = OpenDatabase("atomic.db");

        // Institutions are written first; the invalid investment id fails afterwards and must roll everything back.
        var json = """
            {
              "formatVersion": 2,
              "financialInstitutions": [ { "id": "11111111-1111-1111-1111-111111111111", "name": "Partial", "createdDate": "2026-01-01T00:00:00Z", "updatedDate": "2026-01-01T00:00:00Z" } ],
              "investments": [ { "id": "not-a-guid" } ]
            }
            """;

        await Assert.ThrowsAnyAsync<Exception>(() => CreateExport(db).ImportJsonAsync(AsStream(json)));
        Assert.Empty(await new EfRepository<FinancialInstitution>(db).GetAllAsync());
    }

    [Fact]
    public async Task Unit_of_work_rolls_back_every_write_when_the_work_throws()
    {
        await using var db = OpenDatabase("uow.db");
        var repo = new EfRepository<FinancialInstitution>(db);
        var unit = new UnitOfWork(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => unit.ExecuteAsync(async () =>
        {
            await repo.UpsertAsync(new FinancialInstitution { Id = Guid.NewGuid(), Name = "Temporary", CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow });
            throw new InvalidOperationException("boom");
        }));

        Assert.Empty(await repo.GetAllAsync());
        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Unit_of_work_commits_when_the_work_succeeds()
    {
        await using var db = OpenDatabase("uow-commit.db");
        var repo = new EfRepository<FinancialInstitution>(db);

        await new UnitOfWork(db).ExecuteAsync(async () =>
        {
            await repo.UpsertAsync(new FinancialInstitution { Id = Guid.NewGuid(), Name = "Kept", CreatedDate = DateTime.UtcNow, UpdatedDate = DateTime.UtcNow });
        });

        Assert.Single(await repo.GetAllAsync());
    }

    [Fact]
    public async Task Saving_the_income_profile_replaces_the_previous_one()
    {
        await using var db = OpenDatabase("profile.db");
        var service = new IncomeService(
            new EfRepository<IncomeProfile>(db),
            new EfRepository<ExtraIncome>(db),
            new EfRepository<BusinessDayOverride>(db),
            new UnitOfWork(db));

        await service.SaveProfileAsync(new IncomeProfile { HourlyRateUsd = 10, HoursPerDay = 8, UsdBrlRate = 5, TaxPercent = 0 });
        await service.SaveProfileAsync(new IncomeProfile { HourlyRateUsd = 20, HoursPerDay = 4, UsdBrlRate = 6, TaxPercent = 15 });

        var all = (await new EfRepository<IncomeProfile>(db).GetAllAsync()).ToList();
        var only = Assert.Single(all);
        Assert.Equal(20m, only.HourlyRateUsd);
    }
}
