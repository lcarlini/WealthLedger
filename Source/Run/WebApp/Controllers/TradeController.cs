using System.Net;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Trades;
using WealthLedger.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace WealthLedger.WebApp.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/trades")]
public class TradeController(ITradeLedgerService ledger) : ControllerBase
{
    [HttpGet]
    public async Task<Response<IReadOnlyList<TradeResponse>>> ListAsync([FromQuery] Guid investmentId, CancellationToken ct)
        => await Run(() => ledger.GetTradesAsync(investmentId, ct));

    /// <summary>Position for a holding. method: "weighted" (default, custo médio) or "fifo" (comparison).</summary>
    [HttpGet("position/{investmentId:guid}")]
    public async Task<Response<PositionSummary>> PositionAsync(
        Guid investmentId,
        [FromQuery] string? method,
        CancellationToken ct)
    {
        if (!TryParseMethod(method, out var parsed))
            return Fail<PositionSummary>(new ArgumentException("method must be 'weighted' or 'fifo'."));
        return await Run(() => ledger.GetPositionAsync(investmentId, parsed, ct));
    }

    /// <summary>Realised and unrealised P&amp;L, dividends, and annualised money-weighted return (XIRR).</summary>
    [HttpGet("performance/{investmentId:guid}")]
    public async Task<Response<TradePerformance>> PerformanceAsync(
        Guid investmentId,
        [FromQuery] string? method,
        CancellationToken ct)
    {
        if (!TryParseMethod(method, out var parsed))
            return Fail<TradePerformance>(new ArgumentException("method must be 'weighted' or 'fifo'."));
        return await Run(() => ledger.GetPerformanceAsync(investmentId, parsed, ct));
    }

    /// <summary>Estimated monthly sales tax by asset category for a calendar year.</summary>
    [HttpGet("tax-report")]
    public async Task<Response<TaxReport>> TaxReportAsync([FromQuery] int? year, CancellationToken ct)
    {
        var target = year ?? DateTime.UtcNow.Year;
        if (target < 2000 || target > 2100)
            return Fail<TaxReport>(new ArgumentException("year must be between 2000 and 2100."));
        return await Run(() => ledger.GetTaxReportAsync(target, ct));
    }

    [HttpPost]
    public async Task<Response<TradeResponse>> CreateAsync([FromBody] TradeRequest request, CancellationToken ct)
        => await Run(() => ledger.AddTradeAsync(request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<Response<bool>> DeleteAsync(Guid id, CancellationToken ct)
    {
        try
        {
            await ledger.DeleteTradeAsync(id, ct);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Fail<bool>(ex);
        }
    }

    private static bool TryParseMethod(string? value, out CostMethod method)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case null or "" or "weighted":
                method = CostMethod.WeightedAverage;
                return true;
            case "fifo":
                method = CostMethod.Fifo;
                return true;
            default:
                method = default;
                return false;
        }
    }

    private async Task<Response<T>> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Fail<T>(ex);
        }
    }

    private static Response<T> Fail<T>(Exception ex) =>
        new([new Error(ex.Message)], ex is InvalidOperationException ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest);
}
