using System.Net;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Settings;
using WealthLedger.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace WealthLedger.WebApp.Controllers;

/// <summary>App configuration: AI provider and API keys, market-data tokens. Secrets are write-only.</summary>
[ApiController]
[Produces("application/json")]
[Route("api/settings")]
public class SettingsController(IAppSettingsService settings) : ControllerBase
{
    [HttpGet]
    public async Task<Response<AppSettingsView>> GetAsync(CancellationToken ct)
        => new(await settings.GetViewAsync(ct));

    [HttpPut]
    public async Task<Response<AppSettingsView>> UpdateAsync([FromBody] AppSettingsUpdate update, CancellationToken ct)
    {
        try
        {
            return new Response<AppSettingsView>(await settings.UpdateAsync(update, ct));
        }
        catch (ArgumentException ex)
        {
            return new Response<AppSettingsView>([new Error(ex.Message)], HttpStatusCode.BadRequest);
        }
    }
}
