using WealthLedger.Application.Services;
using WealthLedger.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace WealthLedger.WebApp.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/market-data")]
public class MarketDataController : ControllerBase
{
    private readonly IMarketDataService _marketDataService;

    public MarketDataController(IMarketDataService marketDataService)
    {
        _marketDataService = marketDataService;
    }

    [HttpGet]
    public async Task<Response<Contracts.Api.Responses.MarketDataResponse>> GetAsync()
    {
        return await _marketDataService.GetMarketDataAsync();
    }
}
