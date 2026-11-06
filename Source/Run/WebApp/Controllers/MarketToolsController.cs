using System.Net;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Market;
using WealthLedger.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace WealthLedger.WebApp.Controllers;

/// <summary>Public market information: Tesouro Direto and benchmark indices.</summary>
[ApiController]
[Produces("application/json")]
[Route("api/market-tools")]
public class MarketToolsController(IMarketToolsService market) : ControllerBase
{
    [HttpGet("treasury")]
    public async Task<Response<TreasurySnapshot>> GetTreasuryAsync(CancellationToken ct)
    {
        try
        {
            return new Response<TreasurySnapshot>(await market.GetTreasuryAsync(ct));
        }
        catch (InvalidOperationException ex)
        {
            return new Response<TreasurySnapshot>([new Error(ex.Message)], HttpStatusCode.BadGateway);
        }
    }

    [HttpGet("indices")]
    public async Task<Response<MarketIndicesSnapshot>> GetIndicesAsync(CancellationToken ct)
    {
        try
        {
            return new Response<MarketIndicesSnapshot>(await market.GetIndicesAsync(ct));
        }
        catch (InvalidOperationException ex)
        {
            return new Response<MarketIndicesSnapshot>([new Error(ex.Message)], HttpStatusCode.BadGateway);
        }
    }
}
