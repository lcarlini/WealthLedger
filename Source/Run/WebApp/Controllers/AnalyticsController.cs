using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;
using WealthLedger.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace WealthLedger.WebApp.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly IOfxImportService _ofxService;
    private readonly IRepository<BankTransaction> _transactionRepo;
    private readonly IRepository<StatementImport> _importRepo;

    public AnalyticsController(
        IOfxImportService ofxService,
        IRepository<BankTransaction> transactionRepo,
        IRepository<StatementImport> importRepo)
    {
        _ofxService = ofxService;
        _transactionRepo = transactionRepo;
        _importRepo = importRepo;
    }

    /// <summary>
    /// Returns spending grouped by category and month for a given source (Card or Checking).
    /// </summary>
    [HttpGet("spending-summary")]
    public async Task<Response<SpendingSummaryResponse>> GetSpendingSummary(
        [FromQuery] StatementSource source)
    {
        var imports = (await _importRepo.GetAllAsync())
            .Where(i => i.Source == source)
            .Select(i => i.Id)
            .ToHashSet();

        var transactions = (await _transactionRepo.GetAllAsync())
            .Where(t => imports.Contains(t.StatementImportId) && t.Amount < 0) // expenses only
            .ToList();

        // Group by category
        var byCategory = transactions
            .GroupBy(t => t.Category ?? "Other")
            .Select(g => new CategorySummary
            {
                Category = g.Key,
                TotalAmount = Math.Abs(g.Sum(t => t.Amount)),
                TransactionCount = g.Count(),
            })
            .OrderByDescending(c => c.TotalAmount)
            .ToList();

        // Group by month + category
        var monthlyBreakdown = transactions
            .GroupBy(t => new { t.Date.Year, t.Date.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthCategoryBreakdown
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Categories = g
                    .GroupBy(t => t.Category ?? "Other")
                    .Select(cg => new CategoryAmount
                    {
                        Category = cg.Key,
                        Amount = Math.Abs(cg.Sum(t => t.Amount)),
                    })
                    .OrderByDescending(c => c.Amount)
                    .ToList(),
            })
            .ToList();

        return new Response<SpendingSummaryResponse>(new SpendingSummaryResponse
        {
            Source = source.ToString(),
            ByCategory = byCategory,
            MonthlyBreakdown = monthlyBreakdown,
        });
    }

    /// <summary>
    /// Re-categorizes all transactions using the auto-categorization rules.
    /// </summary>
    [HttpPost("recategorize")]
    public async Task<Response<object>> Recategorize()
    {
        var count = await _ofxService.RecategorizeAllAsync();
        return new Response<object>(new { updated = count });
    }
}

// ─── Response DTOs ──────────────────────────────────────

public class SpendingSummaryResponse
{
    public string Source { get; set; } = string.Empty;
    public List<CategorySummary> ByCategory { get; set; } = [];
    public List<MonthCategoryBreakdown> MonthlyBreakdown { get; set; } = [];
}

public class CategorySummary
{
    public string Category { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int TransactionCount { get; set; }
}

public class MonthCategoryBreakdown
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<CategoryAmount> Categories { get; set; } = [];
}

public class CategoryAmount
{
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
