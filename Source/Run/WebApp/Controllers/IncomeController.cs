using System.Net;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Domain;
using WealthLedger.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace WealthLedger.WebApp.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/income")]
public class IncomeController : ControllerBase
{
    private readonly IIncomeService _service;

    public IncomeController(IIncomeService service)
    {
        _service = service;
    }

    // ─── Profile ────────────────────────────────────────

    [HttpGet("profile")]
    public async Task<Response<IncomeProfile?>> GetProfile()
    {
        return new Response<IncomeProfile?>(await _service.GetProfileAsync());
    }

    [HttpPut("profile")]
    public async Task<Response<IncomeProfile>> SaveProfile([FromBody] IncomeProfileRequest body)
    {
        var profile = new IncomeProfile
        {
            HourlyRateUsd = body.HourlyRateUsd,
            HoursPerDay = body.HoursPerDay,
            UsdBrlRate = body.UsdBrlRate,
            TaxPercent = body.TaxPercent,
        };
        var saved = await _service.SaveProfileAsync(profile);
        return new Response<IncomeProfile>(saved);
    }

    // ─── Extra income ───────────────────────────────────

    [HttpGet("extra")]
    public async Task<Response<IEnumerable<ExtraIncome>>> GetExtra()
    {
        return new Response<IEnumerable<ExtraIncome>>(await _service.GetExtraIncomeAsync());
    }

    [HttpPost("extra")]
    public async Task<Response<ExtraIncome>> AddExtra([FromBody] ExtraIncomeRequest body)
    {
        var entry = new ExtraIncome
        {
            Year = body.Year,
            Month = body.Month,
            Amount = body.Amount,
            Description = body.Description,
        };
        return new Response<ExtraIncome>(await _service.AddExtraIncomeAsync(entry));
    }

    [HttpDelete("extra/{id}")]
    public async Task<Response<object>> DeleteExtra(Guid id)
    {
        await _service.DeleteExtraIncomeAsync(id);
        return new Response<object>(errors: [], statusCode: HttpStatusCode.NoContent);
    }

    // ─── Business day overrides ─────────────────────────

    [HttpPut("business-days")]
    public async Task<Response<object>> SetBusinessDayOverride([FromBody] BusinessDayOverrideRequest body)
    {
        if (body.Days < 0 || body.Days > 31)
            return new Response<object>(errors: ["Days must be between 0 and 31"], statusCode: HttpStatusCode.BadRequest);
        await _service.SetBusinessDayOverrideAsync(body.Year, body.Month, body.Days);
        return new Response<object>(new { success = true });
    }

    [HttpDelete("business-days")]
    public async Task<Response<object>> ResetBusinessDayOverride([FromQuery] int year, [FromQuery] int month)
    {
        await _service.ResetBusinessDayOverrideAsync(year, month);
        return new Response<object>(new { success = true });
    }

    // ─── Preview (business days + breakdown) ────────────

    [HttpGet("preview")]
    public async Task<Response<IncomePreviewResponse>> GetPreview(
        [FromQuery] int fromYear,
        [FromQuery] int fromMonth,
        [FromQuery] int monthCount = 12)
    {
        if (monthCount < 1 || monthCount > 60) monthCount = 12;
        var profile = await _service.GetProfileAsync();
        if (profile == null)
            return new Response<IncomePreviewResponse>(new IncomePreviewResponse());

        var extras = (await _service.GetExtraIncomeAsync()).ToList();
        var overrides = await _service.GetAllOverridesAsync();
        var months = new List<IncomeMonthPreview>();

        var y = fromYear;
        var m = fromMonth;
        for (int i = 0; i < monthCount; i++)
        {
            var defaultDays = _service.GetDefaultBusinessDaysInMonth(y, m);
            var hasOverride = overrides.TryGetValue((y, m), out var overrideDays);
            var effectiveDays = hasOverride ? overrideDays : defaultDays;

            var grossBrl = effectiveDays * profile.HoursPerDay * profile.HourlyRateUsd * profile.UsdBrlRate;
            var taxBrl = Math.Round(grossBrl * profile.TaxPercent / 100m, 2);
            var netBrl = Math.Round(grossBrl - taxBrl, 2);
            grossBrl = Math.Round(grossBrl, 2);

            var extra = extras.Where(e => e.Year == y && e.Month == m).Sum(e => e.Amount);

            months.Add(new IncomeMonthPreview
            {
                Year = y,
                Month = m,
                BusinessDays = effectiveDays,
                DefaultBusinessDays = defaultDays,
                IsBusinessDaysOverridden = hasOverride,
                GrossBrl = grossBrl,
                TaxBrl = taxBrl,
                NetBrl = netBrl,
                ExtraBrl = extra,
                TotalBrl = netBrl + extra,
            });

            m++;
            if (m > 12) { m = 1; y++; }
        }

        return new Response<IncomePreviewResponse>(new IncomePreviewResponse { Months = months });
    }
}

// ─── Request / Response DTOs ────────────────────────────

public class IncomeProfileRequest
{
    public decimal HourlyRateUsd { get; set; }
    public int HoursPerDay { get; set; } = 8;
    public decimal UsdBrlRate { get; set; }
    public decimal TaxPercent { get; set; }
}

public class ExtraIncomeRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class BusinessDayOverrideRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Days { get; set; }
}

public class IncomePreviewResponse
{
    public List<IncomeMonthPreview> Months { get; set; } = [];
}

public class IncomeMonthPreview
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int BusinessDays { get; set; }
    public int DefaultBusinessDays { get; set; }
    public bool IsBusinessDaysOverridden { get; set; }
    public decimal GrossBrl { get; set; }
    public decimal TaxBrl { get; set; }
    public decimal NetBrl { get; set; }
    public decimal ExtraBrl { get; set; }
    public decimal TotalBrl { get; set; }
}
