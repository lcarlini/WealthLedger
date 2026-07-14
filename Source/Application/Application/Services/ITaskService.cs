using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;

namespace WealthLedger.Application.Services;

public interface ITaskService
{
    Task<IEnumerable<TaskItem>> GetPendingTasksAsync();
    Task<IEnumerable<TaskItem>> GetCompletedTasksAsync();
    Task<IEnumerable<FutureTaskResponse>> GetFutureTasksAsync(int monthsAhead = 12);
    Task<TaskItem?> CompleteTaskAsync(Guid taskId);
    Task EnsureMonthlyTasksAsync();
    Task EnsureMaturityTasksAsync();
    Task<int> GetPendingCountAsync();
}
