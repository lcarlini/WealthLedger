using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace WealthLedger.WebApp.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet]
    public async Task<Response<DashboardResponse>> GetAsync([FromQuery] int projectionYears = 3)
    {
        projectionYears = Math.Clamp(projectionYears, 1, 100);
        return await _dashboardService.GetDashboardAsync(projectionYears);
    }
}
