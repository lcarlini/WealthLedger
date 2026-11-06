using System.Net;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Budgets;
using WealthLedger.Contracts.Domain;
using WealthLedger.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace WealthLedger.WebApp.Controllers;

/// <summary>Monthly spending limits per category and how much of each is used.</summary>
[ApiController]
[Produces("application/json")]
[Route("api/budgets")]
public class BudgetController(IBudgetService budgets) : ControllerBase
{
    [HttpGet]
    public async Task<Response<IReadOnlyList<Budget>>> GetAllAsync()
        => new(await budgets.GetAllAsync());

    [HttpGet("categories")]
    public async Task<Response<IReadOnlyList<string>>> GetCategoriesAsync()
        => new(await budgets.GetKnownCategoriesAsync());

    /// <summary>Budgets versus spending for a month. Defaults to the current month.</summary>
    [HttpGet("status")]
    public async Task<Response<BudgetStatus>> GetStatusAsync([FromQuery] int? year, [FromQuery] int? month)
    {
        var now = DateTime.Now;
        try
        {
            return new Response<BudgetStatus>(await budgets.GetStatusAsync(year ?? now.Year, month ?? now.Month));
        }
        catch (ArgumentException ex)
        {
            return new Response<BudgetStatus>([new Error(ex.Message)], HttpStatusCode.BadRequest);
        }
    }

    /// <summary>Creates a budget or updates the limit of the category's existing one.</summary>
    [HttpPut]
    public async Task<Response<Budget>> SaveAsync([FromBody] BudgetRequest request)
    {
        try
        {
            return new Response<Budget>(await budgets.SaveAsync(request));
        }
        catch (ArgumentException ex)
        {
            return new Response<Budget>([new Error(ex.Message)], HttpStatusCode.BadRequest);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<Response<object>> DeleteAsync(Guid id)
    {
        await budgets.DeleteAsync(id);
        return new Response<object>(errors: [], statusCode: HttpStatusCode.NoContent);
    }
}
