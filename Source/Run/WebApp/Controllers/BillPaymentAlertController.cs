using System.Net;
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
[Route("api/bill-alerts")]
public class BillPaymentAlertController : ControllerBase
{
    private readonly IBillPaymentAlertService _service;
    private readonly IPayloadValidator<BillPaymentAlertRequestBody> _validator;
    private readonly IMapper _mapper;

    public BillPaymentAlertController(
        IBillPaymentAlertService service,
        IPayloadValidator<BillPaymentAlertRequestBody> validator,
        IMapper mapper)
    {
        _service = service;
        _validator = validator;
        _mapper = mapper;
    }

    [HttpGet]
    public async Task<Response<IEnumerable<BillPaymentAlertResponse>>> GetAllAsync()
    {
        var list = await _service.GetAllAsync();
        return new Response<IEnumerable<BillPaymentAlertResponse>>(
            list.OrderBy(a => a.Name).Select(MapAlert));
    }

    [HttpGet("{id:guid}")]
    public async Task<Response<BillPaymentAlertResponse>> GetAsync(Guid id)
    {
        var item = await _service.GetAsync(id);
        if (item == null)
            return new Response<BillPaymentAlertResponse>([new Error("Not found")], HttpStatusCode.NotFound);
        return MapAlert(item);
    }

    [HttpPost]
    public async Task<Response<BillPaymentAlertResponse>> CreateAsync([FromBody] BillPaymentAlertRequestBody body)
    {
        var validation = await _validator.ValidateAsync(body);
        if (!validation.IsValid)
            return new Response<BillPaymentAlertResponse>(
                validation.Errors.Select(e => Error.ForField(e.PropertyName, e.ErrorMessage)).ToArray(),
                HttpStatusCode.BadRequest);

        var entity = _mapper.Map<BillPaymentAlert>(body);
        entity.Id = Guid.NewGuid();
        entity.CreatedDate = entity.UpdatedDate = DateTime.UtcNow;
        var created = await _service.UpsertAsync(entity);
        return MapAlert(created);
    }

    [HttpPut("{id:guid}")]
    public async Task<Response<BillPaymentAlertResponse>> UpdateAsync(Guid id, [FromBody] BillPaymentAlertRequestBody body)
    {
        body.Id = id;
        var validation = await _validator.ValidateAsync(body);
        if (!validation.IsValid)
            return new Response<BillPaymentAlertResponse>(
                validation.Errors.Select(e => Error.ForField(e.PropertyName, e.ErrorMessage)).ToArray(),
                HttpStatusCode.BadRequest);

        var existing = await _service.GetAsync(id);
        if (existing == null)
            return new Response<BillPaymentAlertResponse>([new Error("Not found")], HttpStatusCode.NotFound);

        var entity = _mapper.Map<BillPaymentAlert>(body);
        entity.CreatedDate = existing.CreatedDate;
        entity.UpdatedDate = DateTime.UtcNow;
        var updated = await _service.UpsertAsync(entity);
        return MapAlert(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<Response<object>> DeleteAsync(Guid id)
    {
        var existing = await _service.GetAsync(id);
        if (existing == null)
            return new Response<object>([new Error("Not found")], HttpStatusCode.NotFound);

        await _service.DeleteAsync(id);
        return new Response<object>(errors: [], statusCode: HttpStatusCode.NoContent);
    }

    [HttpGet("due-this-month")]
    public async Task<Response<IEnumerable<BillPaymentOccurrenceResponse>>> GetDueThisMonthAsync()
    {
        return new Response<IEnumerable<BillPaymentOccurrenceResponse>>(await _service.GetDueThisMonthAsync());
    }

    [HttpGet("upcoming")]
    public async Task<Response<IEnumerable<BillPaymentOccurrenceResponse>>> GetUpcomingAsync([FromQuery] int monthsAhead = 12)
    {
        monthsAhead = Math.Clamp(monthsAhead, 1, 120);
        return new Response<IEnumerable<BillPaymentOccurrenceResponse>>(
            await _service.GetUpcomingAsync(monthsAhead));
    }

    [HttpGet("past")]
    public async Task<Response<IEnumerable<BillPaymentOccurrenceResponse>>> GetPastAsync()
    {
        return new Response<IEnumerable<BillPaymentOccurrenceResponse>>(await _service.GetPastAsync());
    }

    [HttpGet("pending-count")]
    public async Task<Response<int>> GetPendingCountAsync()
    {
        return new Response<int>(await _service.GetPendingCountAsync());
    }

    [HttpPut("occurrences/{id:guid}/paid")]
    public async Task<Response<BillPaymentOccurrenceResponse>> MarkPaidAsync(Guid id)
    {
        var result = await _service.MarkPaidAsync(id);
        if (result == null)
            return new Response<BillPaymentOccurrenceResponse>([new Error("Occurrence not found")], HttpStatusCode.NotFound);
        return result;
    }

    private BillPaymentAlertResponse MapAlert(BillPaymentAlert alert)
    {
        var response = _mapper.Map<BillPaymentAlertResponse>(alert);
        response.Currency = alert.Currency.ToString();
        response.Recurrence = alert.Recurrence.ToString();
        return response;
    }
}
