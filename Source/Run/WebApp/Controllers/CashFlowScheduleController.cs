using System.Net;
using System.Text;
using AutoMapper;
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
[Route("api/cashflow-schedule")]
public class CashFlowScheduleController : ControllerBase
{
    private readonly ICashFlowScheduleService _service;
    private readonly IPayloadValidator<CashFlowScheduleItemRequestBody> _validator;
    private readonly IMapper _mapper;

    public CashFlowScheduleController(
        ICashFlowScheduleService service,
        IPayloadValidator<CashFlowScheduleItemRequestBody> validator,
        IMapper mapper)
    {
        _service = service;
        _validator = validator;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<Response<IEnumerable<CashFlowScheduleItemResponse>>> GetAllAsync()
    {
        var list = await _service.GetAllAsync();
        return new Response<IEnumerable<CashFlowScheduleItemResponse>>(
            _mapper.Map<IEnumerable<CashFlowScheduleItemResponse>>(list));
    }

    [HttpGet("{id}")]
    public async Task<Response<CashFlowScheduleItemResponse>> GetAsync(Guid id)
    {
        var item = await _service.GetAsync(id);
        if (item == null)
            return new Response<CashFlowScheduleItemResponse>([new Error("Not found")], HttpStatusCode.NotFound);
        return _mapper.Map<CashFlowScheduleItemResponse>(item);
    }

    [HttpPost]
    public async Task<Response<CashFlowScheduleItemResponse>> CreateAsync([FromBody] CashFlowScheduleItemRequestBody body)
    {
        var validation = await _validator.ValidateAsync(body);
        if (!validation.IsValid)
            return new Response<CashFlowScheduleItemResponse>(
                validation.Errors.Select(e => Error.ForField(e.PropertyName, e.ErrorMessage)).ToArray(),
                HttpStatusCode.BadRequest);

        var entity = _mapper.Map<CashFlowScheduleItem>(body);
        entity.Id = Guid.NewGuid();
        entity.CreatedDate = entity.UpdatedDate = DateTime.UtcNow;
        var created = await _service.UpsertAsync(entity);
        return _mapper.Map<CashFlowScheduleItemResponse>(created);
    }

    [HttpPut("{id}")]
    public async Task<Response<CashFlowScheduleItemResponse>> UpdateAsync(Guid id, [FromBody] CashFlowScheduleItemRequestBody body)
    {
        body.Id = id;
        var validation = await _validator.ValidateAsync(body);
        if (!validation.IsValid)
            return new Response<CashFlowScheduleItemResponse>(
                validation.Errors.Select(e => Error.ForField(e.PropertyName, e.ErrorMessage)).ToArray(),
                HttpStatusCode.BadRequest);

        var existing = await _service.GetAsync(id);
        if (existing == null)
            return new Response<CashFlowScheduleItemResponse>([new Error("Not found")], HttpStatusCode.NotFound);

        var entity = _mapper.Map<CashFlowScheduleItem>(body);
        entity.CreatedDate = existing.CreatedDate;
        entity.UpdatedDate = DateTime.UtcNow;
        var updated = await _service.UpsertAsync(entity);
        return _mapper.Map<CashFlowScheduleItemResponse>(updated);
    }

    [HttpDelete("{id}")]
    public async Task<Response<object>> DeleteAsync(Guid id)
    {
        var existing = await _service.GetAsync(id);
        if (existing == null)
            return new Response<object>([new Error("Not found")], HttpStatusCode.NotFound);
        await _service.DeleteAsync(id);
        return new Response<object>(errors: [], statusCode: HttpStatusCode.NoContent);
    }

    [HttpGet("simulation")]
    public async Task<Response<SimulationMatrixResponse>> GetSimulationAsync(
        [FromQuery] int fromYear,
        [FromQuery] int fromMonth,
        [FromQuery] int monthCount = 36,
        [FromQuery] decimal startingBalance = 0)
    {
        if (monthCount < 1 || monthCount > 1200) monthCount = 36;
        return await _service.GetSimulationMatrixAsync(fromYear, fromMonth, monthCount, startingBalance);
    }

    [HttpGet("proposed-card-installments")]
    public async Task<Response<IEnumerable<ProposedCardInstallmentResponse>>> GetProposedCardInstallmentsAsync()
    {
        return new Response<IEnumerable<ProposedCardInstallmentResponse>>(
            await _service.GetProposedCardInstallmentsAsync());
    }

    [HttpPost("from-card/{bankTransactionId}")]
    public async Task<Response<CashFlowScheduleItemResponse>> AddFromCardAsync(Guid bankTransactionId)
    {
        var item = await _service.AddFromCardInstallmentAsync(bankTransactionId);
        if (item == null)
            return new Response<CashFlowScheduleItemResponse>([new Error("Transaction not found or has no installments")], HttpStatusCode.BadRequest);
        return _mapper.Map<CashFlowScheduleItemResponse>(item);
    }

    [HttpPost("from-card/bulk")]
    public async Task<Response<object>> AddAllFromCardAsync()
    {
        var count = await _service.AddAllFromCardInstallmentsAsync();
        return new Response<object>(new { added = count });
    }

    [HttpPost("from-card/bulk-consolidated")]
    public async Task<Response<object>> AddAllFromCardConsolidatedAsync()
    {
        var count = await _service.AddAllFromCardConsolidatedAsync();
        return new Response<object>(new { added = count });
    }

    [HttpGet("export-csv")]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportCsvAsync(
        [FromQuery] int fromYear,
        [FromQuery] int fromMonth,
        [FromQuery] int monthCount = 36,
        [FromQuery] decimal startingBalance = 0)
    {
        if (monthCount < 1 || monthCount > 1200) monthCount = 36;
        var matrix = await _service.GetSimulationMatrixAsync(fromYear, fromMonth, monthCount, startingBalance);
        var sb = new StringBuilder();
        var sep = ",";
        sb.Append("Item");
        foreach (var col in matrix.MonthColumns)
            sb.Append(sep).Append(col.Label);
        sb.AppendLine();

        // Starting balance row
        sb.Append("Starting Balance");
        for (int i = 0; i < matrix.MonthColumns.Count; i++)
            sb.Append(sep).Append(i == 0 ? startingBalance.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "");
        sb.AppendLine();

        foreach (var row in matrix.Rows)
        {
            sb.Append(EscapeCsv(row.Name));
            foreach (var amt in row.AmountsByMonth)
                sb.Append(sep).Append(amt.HasValue ? amt.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "");
            sb.AppendLine();
        }
        sb.Append("Total");
        foreach (var t in matrix.TotalsPerMonth)
            sb.Append(sep).Append(t.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        sb.AppendLine();
        sb.Append("Accumulated");
        foreach (var a in matrix.AccumulatedPerMonth)
            sb.Append(sep).Append(a.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        sb.AppendLine();
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "Control.csv");
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
