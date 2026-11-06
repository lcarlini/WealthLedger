using System.Net;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Advisor;
using WealthLedger.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace WealthLedger.WebApp.Controllers;

/// <summary>AI assistance for investment and spending decisions.</summary>
[ApiController]
[Produces("application/json")]
[Route("api/advisor")]
public class AdvisorController(IAiAdvisorService advisor) : ControllerBase
{
    /// <summary>Asks the cloud provider chosen in Settings.</summary>
    [HttpPost("ask")]
    public async Task<Response<AdvisorAnswer>> AskAsync([FromBody] AdvisorRequest request, CancellationToken ct)
    {
        try
        {
            return new Response<AdvisorAnswer>(await advisor.AskAsync(request, ct));
        }
        catch (ArgumentException ex)
        {
            return new Response<AdvisorAnswer>([new Error(ex.Message)], HttpStatusCode.BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return new Response<AdvisorAnswer>([new Error(ex.Message)], HttpStatusCode.BadGateway);
        }
    }

    /// <summary>Prompt and aggregated figures for the in-browser model; this data never leaves the machine.</summary>
    [HttpGet("context")]
    public async Task<Response<AdvisorContext>> GetContextAsync(
        [FromQuery] string scope = AdvisorScopes.General,
        [FromQuery] string language = "en",
        CancellationToken ct = default)
    {
        try
        {
            return new Response<AdvisorContext>(await advisor.GetContextAsync(scope, language, ct));
        }
        catch (ArgumentException ex)
        {
            return new Response<AdvisorContext>([new Error(ex.Message)], HttpStatusCode.BadRequest);
        }
    }

    /// <summary>Checks the saved key and model with a tiny request that carries no personal data.</summary>
    [HttpPost("test")]
    public async Task<Response<AdvisorAnswer>> TestAsync(CancellationToken ct)
    {
        try
        {
            var probe = new AdvisorRequest("Reply with the single word OK.", AdvisorScopes.General);
            return new Response<AdvisorAnswer>(await advisor.AskAsync(probe with { }, ct));
        }
        catch (InvalidOperationException ex)
        {
            return new Response<AdvisorAnswer>([new Error(ex.Message)], HttpStatusCode.BadGateway);
        }
    }
}
