using WealthLedger.Contracts.Api.Responses;

namespace WealthLedger.Application.Services;

public interface IDashboardService
{
    Task<DashboardResponse> GetDashboardAsync(int projectionYears = 3);
}
