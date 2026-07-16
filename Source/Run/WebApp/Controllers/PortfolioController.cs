using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper;
using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Application.Validators;
using WealthLedger.Contracts.Api.Requests;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;
using WealthLedger.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace WealthLedger.WebApp.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/portfolio")]
public class PortfolioController : ControllerBase
{
    private readonly IPortfolioService _portfolioService;
    private readonly IDataExportService _dataExportService;
    private readonly IMapper _mapper;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public PortfolioController(
        IPortfolioService portfolioService,
        IDataExportService dataExportService,
        IMapper mapper)
    {
        _portfolioService = portfolioService;
        _dataExportService = dataExportService;
        _mapper = mapper;
    }

    [HttpGet("overview")]
    public async Task<Response<PortfolioOverviewResponse>> GetOverviewAsync(
        [FromQuery] int projectionYears = 3,
        CancellationToken cancellationToken = default)
    {
        projectionYears = Math.Clamp(projectionYears, 1, 100);
        return await _portfolioService.GetOverviewAsync(projectionYears, cancellationToken);
    }

    [HttpGet("calendar")]
    public async Task<Response<IEnumerable<CalendarEventItem>>> GetCalendarAsync(
        [FromQuery] int monthsAhead = 12,
        CancellationToken cancellationToken = default)
    {
        monthsAhead = Math.Clamp(monthsAhead, 1, 1200);
        return new Response<IEnumerable<CalendarEventItem>>(
            await _portfolioService.GetCalendarAsync(monthsAhead, cancellationToken));
    }

    [HttpGet("snapshots")]
    public async Task<Response<IEnumerable<PortfolioSnapshotResponse>>> GetSnapshotsAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshots = await _portfolioService.GetSnapshotsAsync(cancellationToken);
        return new Response<IEnumerable<PortfolioSnapshotResponse>>(
            _mapper.Map<IEnumerable<PortfolioSnapshotResponse>>(snapshots));
    }

    [HttpPost("snapshots")]
    public async Task<Response<PortfolioSnapshotResponse>> CaptureSnapshotAsync(
        [FromQuery] string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _portfolioService.CaptureSnapshotAsync(notes, cancellationToken);
        return _mapper.Map<PortfolioSnapshotResponse>(snapshot);
    }

    [HttpGet("export-json")]
    [Produces("application/json")]
    public async Task<IActionResult> ExportJsonAsync(CancellationToken cancellationToken)
    {
        var payload = await _dataExportService.ExportJsonAsync(cancellationToken);
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        return File(bytes, "application/json", $"wealthledger-export-{DateTime.UtcNow:yyyyMMdd}.json");
    }

    [HttpPost("import-json")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52_428_800)]
    public async Task<Response<object>> ImportJsonAsync(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return new Response<object>([new Error("No file uploaded.")], HttpStatusCode.BadRequest);

        await using var stream = file.OpenReadStream();
        await _dataExportService.ImportJsonAsync(stream, cancellationToken);
        return new Response<object>(new { imported = true, fileName = file.FileName });
    }
}

[ApiController]
[Produces("application/json")]
[Route("api/passive-income")]
public class PassiveIncomeController : ControllerBase
{
    private readonly IPassiveIncomeService _service;
    private readonly IPayloadValidator<PassiveIncomeRequestBody> _validator;
    private readonly IRepository<Investment> _investments;
    private readonly IMapper _mapper;

    public PassiveIncomeController(
        IPassiveIncomeService service,
        IPayloadValidator<PassiveIncomeRequestBody> validator,
        IRepository<Investment> investments,
        IMapper mapper)
    {
        _service = service;
        _validator = validator;
        _investments = investments;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<Response<IEnumerable<PassiveIncomeResponse>>> GetAllAsync()
    {
        var items = (await _service.GetAllAsync()).OrderByDescending(i => i.PaymentDate);
        var responses = new List<PassiveIncomeResponse>();
        foreach (var item in items)
        {
            var r = _mapper.Map<PassiveIncomeResponse>(item);
            if (item.InvestmentId.HasValue)
            {
                var inv = await _investments.GetAsync(item.InvestmentId.Value);
                r.InvestmentName = inv?.Name;
            }
            responses.Add(r);
        }
        return new Response<IEnumerable<PassiveIncomeResponse>>(responses);
    }

    [HttpPost]
    public async Task<Response<PassiveIncomeResponse>> CreateAsync([FromBody] PassiveIncomeRequestBody body)
    {
        var validation = await _validator.ValidateAsync(body);
        if (!validation.IsValid)
            return new Response<PassiveIncomeResponse>(
                validation.Errors.Select(e => Error.ForField(e.PropertyName, e.ErrorMessage)).ToList(),
                HttpStatusCode.BadRequest);

        body.Id = Guid.NewGuid();
        var entity = _mapper.Map<PassiveIncome>(body);
        entity.CreatedDate = entity.UpdatedDate = DateTime.UtcNow;
        var saved = await _service.UpsertAsync(entity);
        return _mapper.Map<PassiveIncomeResponse>(saved);
    }

    [HttpPut("{id}")]
    public async Task<Response<PassiveIncomeResponse>> UpdateAsync(Guid id, [FromBody] PassiveIncomeRequestBody body)
    {
        body.Id = id;
        var existing = await _service.GetAsync(id);
        if (existing == null)
            return new Response<PassiveIncomeResponse>([new Error("Not found")], HttpStatusCode.NotFound);

        var validation = await _validator.ValidateAsync(body);
        if (!validation.IsValid)
            return new Response<PassiveIncomeResponse>(
                validation.Errors.Select(e => Error.ForField(e.PropertyName, e.ErrorMessage)).ToList(),
                HttpStatusCode.BadRequest);

        var entity = _mapper.Map<PassiveIncome>(body);
        entity.CreatedDate = existing.CreatedDate;
        entity.UpdatedDate = DateTime.UtcNow;
        var saved = await _service.UpsertAsync(entity);
        return _mapper.Map<PassiveIncomeResponse>(saved);
    }

    [HttpDelete("{id}")]
    public async Task<Response<object>> DeleteAsync(Guid id)
    {
        if (await _service.GetAsync(id) == null)
            return new Response<object>([new Error("Not found")], HttpStatusCode.NotFound);
        await _service.DeleteAsync(id);
        return new Response<object>(errors: [], statusCode: HttpStatusCode.NoContent);
    }
}

[ApiController]
[Produces("application/json")]
[Route("api/goals")]
public class InvestmentGoalController : ControllerBase
{
    private readonly IInvestmentGoalService _service;
    private readonly IPayloadValidator<InvestmentGoalRequestBody> _validator;
    private readonly IMapper _mapper;

    public InvestmentGoalController(
        IInvestmentGoalService service,
        IPayloadValidator<InvestmentGoalRequestBody> validator,
        IMapper mapper)
    {
        _service = service;
        _validator = validator;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<Response<IEnumerable<InvestmentGoalResponse>>> GetAllAsync()
    {
        var items = await _service.GetAllAsync();
        return new Response<IEnumerable<InvestmentGoalResponse>>(items.Select(Enrich));
    }

    [HttpPost]
    public async Task<Response<InvestmentGoalResponse>> CreateAsync([FromBody] InvestmentGoalRequestBody body)
    {
        var validation = await _validator.ValidateAsync(body);
        if (!validation.IsValid)
            return new Response<InvestmentGoalResponse>(
                validation.Errors.Select(e => Error.ForField(e.PropertyName, e.ErrorMessage)).ToList(),
                HttpStatusCode.BadRequest);

        body.Id = Guid.NewGuid();
        var entity = _mapper.Map<InvestmentGoal>(body);
        entity.CreatedDate = entity.UpdatedDate = DateTime.UtcNow;
        var saved = await _service.UpsertAsync(entity);
        return Enrich(saved);
    }

    [HttpPut("{id}")]
    public async Task<Response<InvestmentGoalResponse>> UpdateAsync(Guid id, [FromBody] InvestmentGoalRequestBody body)
    {
        body.Id = id;
        var existing = await _service.GetAsync(id);
        if (existing == null)
            return new Response<InvestmentGoalResponse>([new Error("Not found")], HttpStatusCode.NotFound);

        var validation = await _validator.ValidateAsync(body);
        if (!validation.IsValid)
            return new Response<InvestmentGoalResponse>(
                validation.Errors.Select(e => Error.ForField(e.PropertyName, e.ErrorMessage)).ToList(),
                HttpStatusCode.BadRequest);

        var entity = _mapper.Map<InvestmentGoal>(body);
        entity.CreatedDate = existing.CreatedDate;
        entity.UpdatedDate = DateTime.UtcNow;
        var saved = await _service.UpsertAsync(entity);
        return Enrich(saved);
    }

    [HttpDelete("{id}")]
    public async Task<Response<object>> DeleteAsync(Guid id)
    {
        if (await _service.GetAsync(id) == null)
            return new Response<object>([new Error("Not found")], HttpStatusCode.NotFound);
        await _service.DeleteAsync(id);
        return new Response<object>(errors: [], statusCode: HttpStatusCode.NoContent);
    }

    private InvestmentGoalResponse Enrich(InvestmentGoal g)
    {
        var r = _mapper.Map<InvestmentGoalResponse>(g);
        r.RemainingAmount = Math.Max(0, g.TargetAmount - g.CurrentAmount);
        r.ProgressPercent = g.TargetAmount > 0
            ? Math.Clamp(g.CurrentAmount / g.TargetAmount * 100, 0, 100)
            : 0;

        if (g.TargetDate.HasValue)
        {
            var months = ((g.TargetDate.Value.Year - DateTime.UtcNow.Year) * 12)
                         + g.TargetDate.Value.Month - DateTime.UtcNow.Month;
            r.MonthsRemaining = Math.Max(0, months);

            var monthly = g.MonthlyContribution ?? 0;
            var rate = (g.ExpectedAnnualReturnPercent ?? 0) / 100 / 12;
            var balance = g.CurrentAmount;
            for (var i = 0; i < r.MonthsRemaining; i++)
            {
                balance = balance * (1 + rate) + monthly;
            }
            r.ProjectedAmountAtTarget = balance;
            r.OnTrack = balance >= g.TargetAmount;
        }

        return r;
    }
}

[ApiController]
[Produces("application/json")]
[Route("api/watchlist")]
public class WatchlistController : ControllerBase
{
    private readonly IWatchlistService _service;
    private readonly IStockQuoteService _quotes;
    private readonly IPayloadValidator<WatchlistItemRequestBody> _validator;
    private readonly IMapper _mapper;

    public WatchlistController(
        IWatchlistService service,
        IStockQuoteService quotes,
        IPayloadValidator<WatchlistItemRequestBody> validator,
        IMapper mapper)
    {
        _service = service;
        _quotes = quotes;
        _validator = validator;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<Response<IEnumerable<WatchlistItemResponse>>> GetAllAsync(CancellationToken cancellationToken)
    {
        var items = await _service.GetAllAsync();
        var responses = new List<WatchlistItemResponse>();
        foreach (var item in items)
        {
            var r = _mapper.Map<WatchlistItemResponse>(item);
            try
            {
                var quote = await _quotes.GetQuoteAsync(item.Ticker, item.AccountType, cancellationToken);
                if (quote != null)
                {
                    r.LastPrice = quote.Price;
                    r.LastPriceCurrency = quote.Currency;
                    if (item.AlertAbove is > 0 && quote.Price >= item.AlertAbove)
                    {
                        r.AlertTriggered = true;
                        r.AlertMessage = $"{item.Ticker} is at/above alert {item.AlertAbove}";
                    }
                    else if (item.AlertBelow is > 0 && quote.Price <= item.AlertBelow)
                    {
                        r.AlertTriggered = true;
                        r.AlertMessage = $"{item.Ticker} is at/below alert {item.AlertBelow}";
                    }
                }
            }
            catch
            {
                // quote optional
            }
            responses.Add(r);
        }
        return new Response<IEnumerable<WatchlistItemResponse>>(responses);
    }

    [HttpPost]
    public async Task<Response<WatchlistItemResponse>> CreateAsync([FromBody] WatchlistItemRequestBody body)
    {
        var validation = await _validator.ValidateAsync(body);
        if (!validation.IsValid)
            return new Response<WatchlistItemResponse>(
                validation.Errors.Select(e => Error.ForField(e.PropertyName, e.ErrorMessage)).ToList(),
                HttpStatusCode.BadRequest);

        body.Id = Guid.NewGuid();
        var entity = _mapper.Map<WatchlistItem>(body);
        entity.CreatedDate = entity.UpdatedDate = DateTime.UtcNow;
        var saved = await _service.UpsertAsync(entity);
        return _mapper.Map<WatchlistItemResponse>(saved);
    }

    [HttpPut("{id}")]
    public async Task<Response<WatchlistItemResponse>> UpdateAsync(Guid id, [FromBody] WatchlistItemRequestBody body)
    {
        body.Id = id;
        var existing = await _service.GetAsync(id);
        if (existing == null)
            return new Response<WatchlistItemResponse>([new Error("Not found")], HttpStatusCode.NotFound);

        var validation = await _validator.ValidateAsync(body);
        if (!validation.IsValid)
            return new Response<WatchlistItemResponse>(
                validation.Errors.Select(e => Error.ForField(e.PropertyName, e.ErrorMessage)).ToList(),
                HttpStatusCode.BadRequest);

        var entity = _mapper.Map<WatchlistItem>(body);
        entity.CreatedDate = existing.CreatedDate;
        entity.UpdatedDate = DateTime.UtcNow;
        var saved = await _service.UpsertAsync(entity);
        return _mapper.Map<WatchlistItemResponse>(saved);
    }

    [HttpDelete("{id}")]
    public async Task<Response<object>> DeleteAsync(Guid id)
    {
        if (await _service.GetAsync(id) == null)
            return new Response<object>([new Error("Not found")], HttpStatusCode.NotFound);
        await _service.DeleteAsync(id);
        return new Response<object>(errors: [], statusCode: HttpStatusCode.NoContent);
    }
}
