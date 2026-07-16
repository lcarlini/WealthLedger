using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly IRepository<FinancialInstitution> _institutionRepository;
    private readonly IRepository<Investment> _investmentRepository;
    private readonly IRepository<CashFlowScheduleItem> _scheduleRepository;
    private readonly ITaskService _taskService;
    private readonly IMarketDataService _marketDataService;

    public DashboardService(
        IRepository<FinancialInstitution> institutionRepository,
        IRepository<Investment> investmentRepository,
        IRepository<CashFlowScheduleItem> scheduleRepository,
        ITaskService taskService,
        IMarketDataService marketDataService)
    {
        _institutionRepository = institutionRepository;
        _investmentRepository = investmentRepository;
        _scheduleRepository = scheduleRepository;
        _taskService = taskService;
        _marketDataService = marketDataService;
    }

    public async Task<DashboardResponse> GetDashboardAsync(int projectionYears = 3)
    {
        projectionYears = Math.Clamp(projectionYears, 1, 100);
        var projectionMonths = projectionYears * 12;
        var institutions = (await _institutionRepository.GetAllAsync()).ToList();
        var investments = (await _investmentRepository.GetAllAsync()).ToList();
        var pendingCount = await _taskService.GetPendingCountAsync();

        var marketData = await _marketDataService.GetMarketDataAsync();
        decimal usdBrl = marketData.UsdBrl > 0 ? marketData.UsdBrl : 0;
        decimal eurBrl = marketData.EurBrl > 0 ? marketData.EurBrl : 0;

        decimal ToBrl(Investment i)
        {
            return i.Currency switch
            {
                Currency.USD => i.Amount * usdBrl,
                Currency.EUR => i.Amount * eurBrl,
                _ => i.Amount
            };
        }

        var byType = investments
            .GroupBy(i => i.AccountType)
            .Select(g => new InvestmentsByTypeItem
            {
                AccountType = g.Key.ToString(),
                Count = g.Count(),
                TotalAmount = g.Sum(ToBrl)
            })
            .ToList();

        var scheduleItems = (await _scheduleRepository.GetAllAsync()).ToList();
        var debitTypes = new HashSet<CashFlowItemType>
        {
            CashFlowItemType.Debt,
            CashFlowItemType.Expense,
            CashFlowItemType.CardInstallment
        };

        var now = DateTime.UtcNow;
        var nextMonthDate = new DateTime(now.Year, now.Month, 1).AddMonths(1);
        var nextMonthYear = nextMonthDate.Year;
        var nextMonth = nextMonthDate.Month;

        decimal plannedDebitsProjection = 0;
        for (var i = 0; i < projectionMonths; i++)
        {
            var date = nextMonthDate.AddMonths(i);
            plannedDebitsProjection += scheduleItems
                .Where(item => debitTypes.Contains(item.ItemType) && IsItemActiveInMonth(item, date.Year, date.Month))
                .Sum(item => Math.Abs(item.AmountPerMonth));
        }

        var debitsNextMonth = scheduleItems
            .Where(item => debitTypes.Contains(item.ItemType) && IsItemActiveInMonth(item, nextMonthYear, nextMonth))
            .Sum(item => Math.Abs(item.AmountPerMonth));

        var futureCardDebits = scheduleItems
            .Where(item => item.ItemType == CashFlowItemType.CardInstallment)
            .Sum(item => SumFutureItemAmountFrom(item, nextMonthYear, nextMonth));

        return new DashboardResponse
        {
            InstitutionCount = institutions.Count,
            InvestmentCount = investments.Count,
            TotalAmount = investments.Sum(ToBrl),
            PlannedDebitsProjection = plannedDebitsProjection,
            ProjectionYears = projectionYears,
            DebitsNextMonth = debitsNextMonth,
            FutureCardDebits = futureCardDebits,
            InvestmentsByType = byType,
            PendingTaskCount = pendingCount
        };
    }

    private static decimal SumFutureItemAmountFrom(CashFlowScheduleItem item, int fromYear, int fromMonth)
    {
        decimal total = 0;
        var year = item.StartYear;
        var month = item.StartMonth;
        var remainingMonths = item.NumberOfMonths;
        while (remainingMonths > 0)
        {
            if (year > fromYear || (year == fromYear && month >= fromMonth))
                total += Math.Abs(item.AmountPerMonth);

            month++;
            if (month > 12)
            {
                month = 1;
                year++;
            }
            remainingMonths--;
        }
        return total;
    }

    private static bool IsItemActiveInMonth(CashFlowScheduleItem item, int year, int month)
    {
        var currentYear = item.StartYear;
        var currentMonth = item.StartMonth;
        var remainingMonths = item.NumberOfMonths;
        while (remainingMonths > 0)
        {
            if (currentYear == year && currentMonth == month)
                return true;

            currentMonth++;
            if (currentMonth > 12)
            {
                currentMonth = 1;
                currentYear++;
            }
            remainingMonths--;
        }
        return false;
    }
}
