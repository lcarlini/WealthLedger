using System.Text.Json;
using System.Text.Json.Serialization;
using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Domain;
using Microsoft.Extensions.Logging;

namespace WealthLedger.Infrastructure.Services;

/// <summary>
/// Full-fidelity JSON export/import. Every table is written, and the import runs in one transaction
/// so a bad file can never leave the database half-restored.
/// </summary>
public class DataExportService : IDataExportService
{
    /// <summary>Bump when the shape changes. Files with a newer version are rejected rather than misread.</summary>
    public const int FormatVersion = 2;

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
    private readonly IRepository<BillPaymentAlert> _bills;
    private readonly IRepository<BillPaymentOccurrence> _billOccurrences;
    private readonly IRepository<TradeTransaction> _trades;
    private readonly IUnitOfWork _unitOfWork;
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
        IRepository<BillPaymentAlert> bills,
        IRepository<BillPaymentOccurrence> billOccurrences,
        IRepository<TradeTransaction> trades,
        IUnitOfWork unitOfWork,
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
        _bills = bills;
        _billOccurrences = billOccurrences;
        _trades = trades;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<object> ExportJsonAsync(CancellationToken cancellationToken = default)
    {
        return new
        {
            exportedAt = DateTime.UtcNow,
            formatVersion = FormatVersion,
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
            watchlistItems = await _watchlist.GetAllAsync(),
            billPaymentAlerts = await _bills.GetAllAsync(),
            billPaymentOccurrences = await _billOccurrences.GetAllAsync(),
            tradeTransactions = await _trades.GetAllAsync()
        };
    }

    public async Task ImportJsonAsync(Stream jsonStream, CancellationToken cancellationToken = default)
    {
        using var doc = await JsonDocument.ParseAsync(jsonStream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The file is not a WealthLedger export.");

        if (root.TryGetProperty("formatVersion", out var versionElement) &&
            versionElement.ValueKind == JsonValueKind.Number &&
            versionElement.GetInt32() > FormatVersion)
        {
            throw new InvalidDataException(
                $"This export uses format {versionElement.GetInt32()}, but this version of WealthLedger reads up to {FormatVersion}.");
        }

        // All-or-nothing: either every section is written or the database is left exactly as it was.
        await _unitOfWork.ExecuteAsync(async () =>
        {
            // Parents first, so references to them are valid when children are written.
            await UpsertArrayAsync<FinancialInstitution>(root, "financialInstitutions", _institutions, cancellationToken);
            await UpsertArrayAsync<Investment>(root, "investments", _investments, cancellationToken);
            await UpsertArrayAsync<StatementImport>(root, "statementImports", _imports, cancellationToken);
            await UpsertArrayAsync<BillPaymentAlert>(root, "billPaymentAlerts", _bills, cancellationToken);
            await UpsertArrayAsync<TradeTransaction>(root, "tradeTransactions", _trades, cancellationToken);
            await UpsertArrayAsync<BankTransaction>(root, "bankTransactions", _transactions, cancellationToken);
            await UpsertArrayAsync<BillPaymentOccurrence>(root, "billPaymentOccurrences", _billOccurrences, cancellationToken);
            await UpsertArrayAsync<PassiveIncome>(root, "passiveIncomes", _passiveIncome, cancellationToken);
            await UpsertArrayAsync<InvestmentGoal>(root, "investmentGoals", _goals, cancellationToken);
            await UpsertArrayAsync<PortfolioSnapshot>(root, "portfolioSnapshots", _snapshots, cancellationToken);
            await UpsertArrayAsync<WatchlistItem>(root, "watchlistItems", _watchlist, cancellationToken);
            await UpsertArrayAsync<CashFlowScheduleItem>(root, "cashFlowScheduleItems", _schedule, cancellationToken);
            await UpsertArrayAsync<TaskItem>(root, "taskItems", _tasks, cancellationToken);
            await UpsertArrayAsync<IncomeProfile>(root, "incomeProfiles", _incomeProfiles, cancellationToken);
            await UpsertArrayAsync<ExtraIncome>(root, "extraIncomes", _extraIncome, cancellationToken);
            await UpsertArrayAsync<BusinessDayOverride>(root, "businessDayOverrides", _overrides, cancellationToken);
        }, cancellationToken);

        _logger.LogInformation("JSON import completed");
    }

    private async Task UpsertArrayAsync<T>(JsonElement root, string property, IRepository<T> repository, CancellationToken ct)
        where T : class, WealthLedger.Contracts.Domain.Interfaces.IEntity
    {
        if (!root.TryGetProperty(property, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return;

        foreach (var element in arr.EnumerateArray())
        {
            ct.ThrowIfCancellationRequested();
            var item = element.Deserialize<T>(JsonOptions)
                       ?? throw new InvalidDataException($"Invalid entry in '{property}'.");
            await repository.UpsertAsync(item);
        }
    }
}
