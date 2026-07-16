using System.Text.Json;
using System.Text.Json.Serialization;
using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Domain;
using Microsoft.Extensions.Logging;

namespace WealthLedger.Infrastructure.Services;

public class DataExportService : IDataExportService
{
    private readonly IRepository<FinancialInstitution> _institutions;
    private readonly IRepository<Investment> _investments;
    private readonly IRepository<TaskItem> _tasks;
    private readonly IRepository<StatementImport> _imports;
    private readonly IRepository<BankTransaction> _transactions;
    private readonly IRepository<CashFlowScheduleItem> _schedule;
    private readonly IRepository<IncomeProfile> _incomeProfiles;
    private readonly IRepository<ExtraIncome> _extraIncome;
    private readonly IRepository<BusinessDayOverride> _overrides;
    private readonly IRepository<PassiveIncome> _passiveIncome;
    private readonly IRepository<InvestmentGoal> _goals;
    private readonly IRepository<PortfolioSnapshot> _snapshots;
    private readonly IRepository<WatchlistItem> _watchlist;
    private readonly ILogger<DataExportService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public DataExportService(
        IRepository<FinancialInstitution> institutions,
        IRepository<Investment> investments,
        IRepository<TaskItem> tasks,
        IRepository<StatementImport> imports,
        IRepository<BankTransaction> transactions,
        IRepository<CashFlowScheduleItem> schedule,
        IRepository<IncomeProfile> incomeProfiles,
        IRepository<ExtraIncome> extraIncome,
        IRepository<BusinessDayOverride> overrides,
        IRepository<PassiveIncome> passiveIncome,
        IRepository<InvestmentGoal> goals,
        IRepository<PortfolioSnapshot> snapshots,
        IRepository<WatchlistItem> watchlist,
        ILogger<DataExportService> logger)
    {
        _institutions = institutions;
        _investments = investments;
        _tasks = tasks;
        _imports = imports;
        _transactions = transactions;
        _schedule = schedule;
        _incomeProfiles = incomeProfiles;
        _extraIncome = extraIncome;
        _overrides = overrides;
        _passiveIncome = passiveIncome;
        _goals = goals;
        _snapshots = snapshots;
        _watchlist = watchlist;
        _logger = logger;
    }

    public async Task<object> ExportJsonAsync(CancellationToken cancellationToken = default)
    {
        return new
        {
            exportedAt = DateTime.UtcNow,
            formatVersion = 1,
            application = "WealthLedger",
            financialInstitutions = await _institutions.GetAllAsync(),
            investments = await _investments.GetAllAsync(),
            taskItems = await _tasks.GetAllAsync(),
            statementImports = await _imports.GetAllAsync(),
            bankTransactions = await _transactions.GetAllAsync(),
            cashFlowScheduleItems = await _schedule.GetAllAsync(),
            incomeProfiles = await _incomeProfiles.GetAllAsync(),
            extraIncomes = await _extraIncome.GetAllAsync(),
            businessDayOverrides = await _overrides.GetAllAsync(),
            passiveIncomes = await _passiveIncome.GetAllAsync(),
            investmentGoals = await _goals.GetAllAsync(),
            portfolioSnapshots = await _snapshots.GetAllAsync(),
            watchlistItems = await _watchlist.GetAllAsync()
        };
    }

    public async Task ImportJsonAsync(Stream jsonStream, CancellationToken cancellationToken = default)
    {
        using var doc = await JsonDocument.ParseAsync(jsonStream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        await UpsertArrayAsync(root, "financialInstitutions", async (e) =>
        {
            var item = e.Deserialize<FinancialInstitution>(JsonOptions);
            if (item != null) await _institutions.UpsertAsync(item);
        });
        await UpsertArrayAsync(root, "investments", async (e) =>
        {
            var item = e.Deserialize<Investment>(JsonOptions);
            if (item != null) await _investments.UpsertAsync(item);
        });
        await UpsertArrayAsync(root, "passiveIncomes", async (e) =>
        {
            var item = e.Deserialize<PassiveIncome>(JsonOptions);
            if (item != null) await _passiveIncome.UpsertAsync(item);
        });
        await UpsertArrayAsync(root, "investmentGoals", async (e) =>
        {
            var item = e.Deserialize<InvestmentGoal>(JsonOptions);
            if (item != null) await _goals.UpsertAsync(item);
        });
        await UpsertArrayAsync(root, "portfolioSnapshots", async (e) =>
        {
            var item = e.Deserialize<PortfolioSnapshot>(JsonOptions);
            if (item != null) await _snapshots.UpsertAsync(item);
        });
        await UpsertArrayAsync(root, "watchlistItems", async (e) =>
        {
            var item = e.Deserialize<WatchlistItem>(JsonOptions);
            if (item != null) await _watchlist.UpsertAsync(item);
        });
        await UpsertArrayAsync(root, "cashFlowScheduleItems", async (e) =>
        {
            var item = e.Deserialize<CashFlowScheduleItem>(JsonOptions);
            if (item != null) await _schedule.UpsertAsync(item);
        });
        await UpsertArrayAsync(root, "taskItems", async (e) =>
        {
            var item = e.Deserialize<TaskItem>(JsonOptions);
            if (item != null) await _tasks.UpsertAsync(item);
        });

        _logger.LogInformation("JSON import completed");
    }

    private static async Task UpsertArrayAsync(JsonElement root, string property, Func<JsonElement, Task> upsert)
    {
        if (!root.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return;
        foreach (var el in arr.EnumerateArray())
            await upsert(el);
    }
}
