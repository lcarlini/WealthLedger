using System.Globalization;
using System.Text.RegularExpressions;
using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Infrastructure.Services;

public class CashFlowScheduleService : ICashFlowScheduleService
{
    private readonly IRepository<CashFlowScheduleItem> _repository;
    private readonly IRepository<BankTransaction> _transactionRepository;
    private readonly IIncomeService _incomeService;

    private static readonly string[] MonthLabels =
        { "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez" };

    public CashFlowScheduleService(
        IRepository<CashFlowScheduleItem> repository,
        IRepository<BankTransaction> transactionRepository,
        IIncomeService incomeService)
    {
        _repository = repository;
        _transactionRepository = transactionRepository;
        _incomeService = incomeService;
    }

    public Task<IEnumerable<CashFlowScheduleItem>> GetAllAsync() => _repository.GetAllAsync();
    public Task<CashFlowScheduleItem?> GetAsync(Guid id) => _repository.GetAsync(id);
    public Task<CashFlowScheduleItem> UpsertAsync(CashFlowScheduleItem entity) => _repository.UpsertAsync(entity);
    public Task DeleteAsync(Guid id) => _repository.DeleteAsync(id);

    public async Task<SimulationMatrixResponse> GetSimulationMatrixAsync(int fromYear, int fromMonth, int monthCount, decimal startingBalance = 0)
    {
        var items = (await _repository.GetAllAsync()).OrderBy(i => i.DisplayOrder).ThenBy(i => i.Name).ToList();
        var columns = new List<SimulationMonthColumn>();
        var y = fromYear;
        var m = fromMonth;
        for (int i = 0; i < monthCount; i++)
        {
            columns.Add(new SimulationMonthColumn
            {
                Year = y,
                Month = m,
                Label = $"{MonthLabels[m - 1]} {y}"
            });
            m++;
            if (m > 12) { m = 1; y++; }
        }

        var rows = new List<SimulationRow>();

        // ── Income rows (computed from IncomeProfile) ──
        var incomeProfile = await _incomeService.GetProfileAsync();
        if (incomeProfile != null)
        {
            var grossAmounts = new List<decimal?>();
            var taxAmounts = new List<decimal?>();

            var iy = fromYear;
            var im = fromMonth;
            for (int i = 0; i < monthCount; i++)
            {
                var (gross, tax, _) = await _incomeService.ComputeMonthlyIncomeAsync(iy, im, incomeProfile);
                grossAmounts.Add(gross);
                taxAmounts.Add(-tax);
                im++;
                if (im > 12) { im = 1; iy++; }
            }

            rows.Add(new SimulationRow
            {
                Id = Guid.Empty,
                Name = "Renda Bruta",
                ItemType = "IncomeGross",
                AmountsByMonth = grossAmounts
            });
            rows.Add(new SimulationRow
            {
                Id = Guid.Empty,
                Name = "Impostos Renda",
                ItemType = "IncomeTax",
                AmountsByMonth = taxAmounts
            });

            // Extra income entries
            var extras = (await _incomeService.GetExtraIncomeAsync()).ToList();
            if (extras.Count > 0)
            {
                foreach (var extra in extras)
                {
                    var extraAmounts = new List<decimal?>();
                    var ey = fromYear;
                    var em = fromMonth;
                    for (int i = 0; i < monthCount; i++)
                    {
                        extraAmounts.Add(ey == extra.Year && em == extra.Month ? extra.Amount : (decimal?)null);
                        em++;
                        if (em > 12) { em = 1; ey++; }
                    }
                    rows.Add(new SimulationRow
                    {
                        Id = extra.Id,
                        Name = $"Extra: {extra.Description}",
                        ItemType = "ExtraIncome",
                        AmountsByMonth = extraAmounts
                    });
                }
            }
        }

        // ── Schedule items ──
        foreach (var item in items)
        {
            var amounts = new List<decimal?>();
            var itemEndYear = fromYear;
            var itemEndMonth = fromMonth;
            var n = item.NumberOfMonths;
            var sm = item.StartMonth;
            var sy = item.StartYear;
            while (n > 0)
            {
                itemEndMonth = sm;
                itemEndYear = sy;
                sm++;
                if (sm > 12) { sm = 1; sy++; }
                n--;
            }

            var colY = fromYear;
            var colM = fromMonth;
            for (int i = 0; i < monthCount; i++)
            {
                decimal? val = null;
                if (IsInRange(colY, colM, item.StartYear, item.StartMonth, itemEndYear, itemEndMonth))
                    val = item.AmountPerMonth;
                amounts.Add(val);
                colM++;
                if (colM > 12) { colM = 1; colY++; }
            }

            rows.Add(new SimulationRow
            {
                Id = item.Id,
                Name = item.Name,
                ItemType = item.ItemType.ToString(),
                AmountsByMonth = amounts
            });
        }

        var totalsPerMonth = new List<decimal>();
        var accumulated = new List<decimal>();
        decimal acc = startingBalance;
        for (int i = 0; i < monthCount; i++)
        {
            decimal sum = rows.Sum(r => r.AmountsByMonth[i] ?? 0);
            totalsPerMonth.Add(sum);
            acc += sum;
            accumulated.Add(acc);
        }

        return new SimulationMatrixResponse
        {
            StartingBalance = startingBalance,
            MonthColumns = columns,
            Rows = rows,
            TotalsPerMonth = totalsPerMonth,
            AccumulatedPerMonth = accumulated
        };
    }

    private static readonly Regex ParcelaRegex =
        new(@"\s*-?\s*[Pp]arcela\s+\d+\s*/\s*\d+\s*$", RegexOptions.Compiled);

    public async Task<IEnumerable<ProposedCardInstallmentResponse>> GetProposedCardInstallmentsAsync()
    {
        var all = await _transactionRepository.GetAllAsync();
        var now = DateTime.UtcNow;

        // Find all transactions that have installment info and still have remaining payments
        var withInstallments = all
            .Where(t => t.InstallmentTotal.HasValue && t.InstallmentTotal.Value > 0
                        && t.InstallmentNumber.HasValue && t.InstallmentNumber.Value > 0
                        && t.InstallmentNumber.Value < t.InstallmentTotal.Value) // still has remaining
            .ToList();

        // For each unique installment series, keep the one with the highest installment number
        // (most recent billing). Group by cleaned description + total + approximate amount.
        var grouped = withInstallments
            .GroupBy(t => new
            {
                CleanDesc = CleanInstallmentDescription(t.Description),
                t.InstallmentTotal,
                AbsAmount = Math.Round(Math.Abs(t.Amount), 2)
            })
            .Select(g =>
            {
                // Pick the transaction with the highest installment number (latest billed)
                var latest = g.OrderByDescending(x => x.InstallmentNumber).ThenByDescending(x => x.Date).First();
                var remaining = latest.InstallmentTotal!.Value - latest.InstallmentNumber!.Value;

                // Start month = next month after the latest transaction date
                var startDate = latest.Date.AddMonths(1);

                return new ProposedCardInstallmentResponse
                {
                    BankTransactionId = latest.Id,
                    Description = g.Key.CleanDesc,
                    AmountPerMonth = Math.Abs(latest.Amount),
                    StartYear = startDate.Year,
                    StartMonth = startDate.Month,
                    RemainingInstallments = remaining,
                    InstallmentNumber = latest.InstallmentNumber ?? 0,
                    InstallmentTotal = latest.InstallmentTotal ?? 0
                };
            })
            .Where(p => p.RemainingInstallments > 0)
            .OrderByDescending(p => p.AmountPerMonth)
            .ToList();

        return grouped;
    }

    /// <summary>
    /// Strips "- Parcela X/Y" suffix from descriptions for cleaner display.
    /// </summary>
    private static string CleanInstallmentDescription(string description)
    {
        return ParcelaRegex.Replace(description, "").Trim();
    }

    public async Task<CashFlowScheduleItem?> AddFromCardInstallmentAsync(Guid bankTransactionId)
    {
        var tx = await _transactionRepository.GetAsync(bankTransactionId);
        if (tx == null || !tx.InstallmentTotal.HasValue || !tx.InstallmentNumber.HasValue) return null;

        var remaining = tx.InstallmentTotal.Value - tx.InstallmentNumber.Value;
        if (remaining <= 0) return null;

        // Start month = next month after the transaction date
        var startDate = tx.Date.AddMonths(1);
        var amount = tx.Amount < 0 ? tx.Amount : -tx.Amount;
        var cleanName = CleanInstallmentDescription(tx.Description);
        if (cleanName.Length > 50) cleanName = cleanName[..50];

        var existingItems = (await _repository.GetAllAsync()).ToList();
        var order = existingItems.Count > 0 ? existingItems.Max(i => i.DisplayOrder) + 1 : 0;

        var item = new CashFlowScheduleItem
        {
            Name = cleanName,
            ItemType = CashFlowItemType.CardInstallment,
            AmountPerMonth = amount,
            StartYear = startDate.Year,
            StartMonth = startDate.Month,
            NumberOfMonths = remaining,
            Source = CashFlowSource.FromCardImport,
            BankTransactionId = bankTransactionId,
            DisplayOrder = order,
            CreatedDate = DateTime.UtcNow,
            UpdatedDate = DateTime.UtcNow
        };
        return await _repository.UpsertAsync(item);
    }

    public async Task<int> AddAllFromCardInstallmentsAsync()
    {
        var proposed = (await GetProposedCardInstallmentsAsync()).ToList();
        int count = 0;
        foreach (var p in proposed)
        {
            var result = await AddFromCardInstallmentAsync(p.BankTransactionId);
            if (result != null) count++;
        }
        return count;
    }

    public async Task<int> AddAllFromCardConsolidatedAsync()
    {
        var proposed = (await GetProposedCardInstallmentsAsync()).ToList();
        if (proposed.Count == 0) return 0;

        // Build a month-by-month total for all proposed installments
        var monthTotals = new SortedDictionary<(int year, int month), decimal>();
        foreach (var p in proposed)
        {
            var y = p.StartYear;
            var m = p.StartMonth;
            for (int i = 0; i < p.RemainingInstallments; i++)
            {
                var key = (y, m);
                if (!monthTotals.ContainsKey(key)) monthTotals[key] = 0;
                monthTotals[key] -= p.AmountPerMonth; // negative for expenses
                m++;
                if (m > 12) { m = 1; y++; }
            }
        }

        if (monthTotals.Count == 0) return 0;

        // Group consecutive months with the same total into single items
        var existingItems = (await _repository.GetAllAsync()).ToList();
        var order = existingItems.Count > 0 ? existingItems.Max(i => i.DisplayOrder) + 1 : 0;
        var entries = monthTotals.ToList();
        int created = 0;
        int idx = 0;
        while (idx < entries.Count)
        {
            var startKey = entries[idx].Key;
            var amount = entries[idx].Value;
            int count = 1;
            // Group consecutive months with same amount
            while (idx + count < entries.Count)
            {
                var next = entries[idx + count];
                var expectedY = startKey.year;
                var expectedM = startKey.month + count;
                while (expectedM > 12) { expectedM -= 12; expectedY++; }
                if (next.Key.year == expectedY && next.Key.month == expectedM && next.Value == amount)
                    count++;
                else
                    break;
            }

            var item = new CashFlowScheduleItem
            {
                Name = "Cartao de Credito",
                ItemType = CashFlowItemType.CardInstallment,
                AmountPerMonth = amount,
                StartYear = startKey.year,
                StartMonth = startKey.month,
                NumberOfMonths = count,
                Source = CashFlowSource.FromCardImport,
                DisplayOrder = order++,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow,
            };
            await _repository.UpsertAsync(item);
            created++;
            idx += count;
        }

        return created;
    }

    private static bool IsInRange(int colY, int colM, int startY, int startM, int endY, int endM)
    {
        if (colY < startY || (colY == startY && colM < startM)) return false;
        if (colY > endY || (colY == endY && colM > endM)) return false;
        return true;
    }
}
