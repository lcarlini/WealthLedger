using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Infrastructure.Services;

public class TaskService : ITaskService
{
    private const int MaturityTaskMaterializeDaysBefore = 45;
    private const int MaturityTaskKeepOverdueDaysAfter = 30;

    private readonly IRepository<TaskItem> _taskRepository;
    private readonly IRepository<Investment> _investmentRepository;
    private readonly IRepository<FinancialInstitution> _institutionRepository;

    public TaskService(
        IRepository<TaskItem> taskRepository,
        IRepository<Investment> investmentRepository,
        IRepository<FinancialInstitution> institutionRepository)
    {
        _taskRepository = taskRepository;
        _investmentRepository = investmentRepository;
        _institutionRepository = institutionRepository;
    }

    public async Task<IEnumerable<TaskItem>> GetPendingTasksAsync()
    {
        await EnsureScheduledTasksAsync();
        var all = await _taskRepository.GetAllAsync();
        var investments = (await _investmentRepository.GetAllAsync()).ToDictionary(i => i.Id);
        var today = DateTime.UtcNow.Date;
        var windowEnd = today.AddDays(7);

        return DeduplicateScheduledTasks(all.Where(t => !t.IsCompleted))
            .Where(t =>
            {
                var dueDate = GetTaskDueDate(t, investments.TryGetValue(t.InvestmentId, out var inv) ? inv : null);
                return IsPendingTask(dueDate, today, windowEnd);
            })
            .OrderBy(t => GetTaskDueDate(t, investments.TryGetValue(t.InvestmentId, out var inv2) ? inv2 : null));
    }

    public async Task<IEnumerable<TaskItem>> GetCompletedTasksAsync()
    {
        var all = await _taskRepository.GetAllAsync();
        return DeduplicateScheduledTasks(all.Where(t => t.IsCompleted))
            .OrderByDescending(t => t.CompletedDate);
    }

    public async Task<IEnumerable<FutureTaskResponse>> GetFutureTasksAsync(int monthsAhead = 36)
    {
        await EnsureScheduledTasksAsync();
        var now = DateTime.UtcNow;
        var today = now.Date;
        var investments = (await _investmentRepository.GetAllAsync()).ToList();
        var institutions = (await _institutionRepository.GetAllAsync()).ToList();
        var allTasks = (await _taskRepository.GetAllAsync()).ToList();

        var futureTasks = new List<FutureTaskResponse>();

        var maturityInvMonthKeys = allTasks
            .Where(t => t.Title.StartsWith("Maturity:", StringComparison.OrdinalIgnoreCase))
            .Select(t => (t.InvestmentId, t.DueYear, t.DueMonth))
            .ToHashSet();

        // 1) Virtual maturity rows (only when no task row for this investment + maturity month yet)
        foreach (var inv in investments.Where(i => i.MaturityDate.HasValue))
        {
            var matDate = ToUtcDateOnly(inv.MaturityDate!.Value);
            var key = (inv.Id, matDate.Year, matDate.Month);
            if (maturityInvMonthKeys.Contains(key))
                continue;

            if (matDate < today)
                continue;

            futureTasks.Add(new FutureTaskResponse
            {
                Title = $"Maturity: {inv.Name}",
                Description = DescribeMaturityAmount(inv),
                InvestmentName = inv.Name,
                InstitutionName = institutions.FirstOrDefault(inst => inst.Id == inv.FinancialInstitutionId)?.Name,
                DueYear = matDate.Year,
                DueMonth = matDate.Month,
                DueDay = matDate.Day,
                Amount = inv.Amount,
                Currency = inv.Currency.ToString(),
                TaskType = "Maturity"
            });
        }

        // 2) Monthly movement tasks for future months (not yet created) — only keys from monthly tasks
        var requiresMovement = investments.Where(i => i.RequiresMonthlyMovement).ToList();
        var monthlyExistingKeys = allTasks
            .Where(t => t.Title.StartsWith("Monthly movement:", StringComparison.OrdinalIgnoreCase))
            .Select(t => (t.InvestmentId, t.DueYear, t.DueMonth))
            .ToHashSet();

        var endDate = now.AddMonths(monthsAhead);
        foreach (var inv in requiresMovement)
        {
            var institution = institutions.FirstOrDefault(inst => inst.Id == inv.FinancialInstitutionId);

            var cursor = new DateTime(now.Year, now.Month, 1).AddMonths(1);
            while (cursor <= endDate)
            {
                var key = (inv.Id, cursor.Year, cursor.Month);
                if (!monthlyExistingKeys.Contains(key))
                {
                    futureTasks.Add(new FutureTaskResponse
                    {
                        Title = $"Monthly movement: {inv.Name}",
                        Description =
                            $"Move R${inv.MonthlyMovementAmount:N2} to maintain rate ({inv.CdiPercentage}% CDI)",
                        InvestmentName = inv.Name,
                        InstitutionName = institution?.Name,
                        DueYear = cursor.Year,
                        DueMonth = cursor.Month,
                        Amount = inv.MonthlyMovementAmount,
                        Currency = inv.Currency.ToString(),
                        TaskType = "MonthlyMovement"
                    });
                }

                cursor = cursor.AddMonths(1);
            }
        }

        return futureTasks
            .OrderBy(t => t.DueYear)
            .ThenBy(t => t.DueMonth)
            .ThenBy(t => t.DueDay ?? 0);
    }

    public async Task<TaskItem?> CompleteTaskAsync(Guid taskId)
    {
        var task = await _taskRepository.GetAsync(taskId);
        if (task == null) return null;

        task.IsCompleted = true;
        task.CompletedDate = DateTime.UtcNow;
        task.UpdatedDate = DateTime.UtcNow;
        return await _taskRepository.UpsertAsync(task);
    }

    public async Task EnsureMonthlyTasksAsync()
    {
        var now = DateTime.UtcNow;
        var investments = await _investmentRepository.GetAllAsync();
        var requiresMovement = investments.Where(i => i.RequiresMonthlyMovement);

        var allTasks = await _taskRepository.GetAllAsync();
        var existingKeys = allTasks
            .Where(t => t.Title.StartsWith("Monthly movement:", StringComparison.OrdinalIgnoreCase))
            .Select(t => (t.InvestmentId, t.DueYear, t.DueMonth))
            .ToHashSet();

        foreach (var inv in requiresMovement)
        {
            var key = (inv.Id, now.Year, now.Month);
            if (existingKeys.Contains(key)) continue;

            var task = new TaskItem
            {
                InvestmentId = inv.Id,
                Title = $"Monthly movement: {inv.Name}",
                Description = $"Move R${inv.MonthlyMovementAmount:N2} to maintain rate ({inv.CdiPercentage}% CDI)",
                DueYear = now.Year,
                DueMonth = now.Month,
                IsCompleted = false,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            await _taskRepository.UpsertAsync(task);
            existingKeys.Add(key);
        }
    }

    public async Task EnsureMaturityTasksAsync()
    {
        var today = DateTime.UtcNow.Date;
        var investments = (await _investmentRepository.GetAllAsync()).ToList();
        var allTasks = (await _taskRepository.GetAllAsync()).ToList();

        foreach (var inv in investments.Where(i =>
                     i.AccountType == AccountType.FixedTerm && i.MaturityDate.HasValue))
        {
            var matDate = ToUtcDateOnly(inv.MaturityDate!.Value);

            var hasMaturityForCycle = allTasks.Any(t =>
                t.InvestmentId == inv.Id &&
                t.Title.StartsWith("Maturity:", StringComparison.OrdinalIgnoreCase) &&
                t.DueYear == matDate.Year &&
                t.DueMonth == matDate.Month);

            if (hasMaturityForCycle)
                continue;

            var windowStart = matDate.AddDays(-MaturityTaskMaterializeDaysBefore);
            var windowEnd = matDate.AddDays(MaturityTaskKeepOverdueDaysAfter);
            if (today < windowStart || today > windowEnd)
                continue;

            var task = new TaskItem
            {
                InvestmentId = inv.Id,
                Title = $"Maturity: {inv.Name}",
                Description = $"Investment matures on {matDate:yyyy-MM-dd}. {DescribeMaturityAmount(inv)}",
                DueYear = matDate.Year,
                DueMonth = matDate.Month,
                IsCompleted = false,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };
            await _taskRepository.UpsertAsync(task);
            allTasks.Add(task);
        }
    }

    public async Task<int> GetPendingCountAsync()
    {
        await EnsureScheduledTasksAsync();
        var all = await _taskRepository.GetAllAsync();
        var investments = (await _investmentRepository.GetAllAsync()).ToDictionary(i => i.Id);
        var today = DateTime.UtcNow.Date;
        var windowEnd = today.AddDays(7);

        return DeduplicateScheduledTasks(all.Where(t => !t.IsCompleted)).Count(t =>
        {
            var dueDate = GetTaskDueDate(t, investments.TryGetValue(t.InvestmentId, out var inv) ? inv : null);
            return IsPendingTask(dueDate, today, windowEnd);
        });
    }

    private static bool IsPendingTask(DateTime dueDate, DateTime today, DateTime windowEnd)
    {
        if (dueDate < today || dueDate <= windowEnd)
            return true;

        return dueDate.Year == today.Year && dueDate.Month == today.Month;
    }

    private async Task EnsureScheduledTasksAsync()
    {
        await RemoveDuplicateScheduledTasksAsync();
        await EnsureMonthlyTasksAsync();
        await EnsureMaturityTasksAsync();
    }

    private async Task RemoveDuplicateScheduledTasksAsync()
    {
        var allTasks = (await _taskRepository.GetAllAsync()).ToList();
        var toDelete = new List<Guid>();

        foreach (var group in allTasks.Where(IsScheduledTask).GroupBy(GetScheduledTaskKey))
        {
            if (group.Count() <= 1)
                continue;

            var keeper = group
                .OrderBy(t => t.IsCompleted)
                .ThenBy(t => t.CreatedDate)
                .First();

            toDelete.AddRange(group.Where(t => t.Id != keeper.Id).Select(t => t.Id));
        }

        foreach (var id in toDelete)
            await _taskRepository.DeleteAsync(id);
    }

    private static IEnumerable<TaskItem> DeduplicateScheduledTasks(IEnumerable<TaskItem> tasks)
    {
        return tasks
            .GroupBy(GetScheduledTaskKey)
            .Select(g => g.OrderBy(t => t.IsCompleted).ThenBy(t => t.CreatedDate).First());
    }

    private static (Guid InvestmentId, int DueYear, int DueMonth, string Kind) GetScheduledTaskKey(TaskItem task)
    {
        var kind = task.Title.StartsWith("Monthly movement:", StringComparison.OrdinalIgnoreCase)
            ? "MonthlyMovement"
            : task.Title.StartsWith("Maturity:", StringComparison.OrdinalIgnoreCase)
                ? "Maturity"
                : task.Title;
        return (task.InvestmentId, task.DueYear, task.DueMonth, kind);
    }

    private static bool IsScheduledTask(TaskItem task) =>
        task.Title.StartsWith("Monthly movement:", StringComparison.OrdinalIgnoreCase) ||
        task.Title.StartsWith("Maturity:", StringComparison.OrdinalIgnoreCase);

    private static DateTime GetTaskDueDate(TaskItem task, Investment? investment)
    {
        if (investment != null && task.Title.StartsWith("Maturity:", StringComparison.OrdinalIgnoreCase) && investment.MaturityDate.HasValue)
        {
            return ToUtcDateOnly(investment.MaturityDate.Value);
        }

        if (investment != null && task.Title.StartsWith("Monthly movement:", StringComparison.OrdinalIgnoreCase))
        {
            var lastDay = DateTime.DaysInMonth(task.DueYear, task.DueMonth);
            return new DateTime(task.DueYear, task.DueMonth, lastDay);
        }

        return new DateTime(task.DueYear, task.DueMonth, 1);
    }

    private static DateTime ToUtcDateOnly(DateTime maturityDate)
    {
        var utc = maturityDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(maturityDate, DateTimeKind.Utc)
            : maturityDate.ToUniversalTime();
        return utc.Date;
    }

    private static string DescribeMaturityAmount(Investment inv)
    {
        var code = inv.Currency.ToString();
        return $"Amount: {code} {inv.Amount:N2}";
    }
}
