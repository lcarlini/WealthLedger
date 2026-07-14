using System.Net;
using AutoMapper;
using WealthLedger.Application.Services;
using WealthLedger.Application.Validators;
using WealthLedger.Contracts.Api.Queries;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Interfaces;
using WealthLedger.Contracts.Domain.Pagination;
using WealthLedger.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace WealthLedger.WebApp.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class BaseEntityController<TDomain, TRequestBody, TResponse> : ControllerBase
    where TDomain : BaseEntity, new()
    where TRequestBody : class, IEntityRequestBody, new()
    where TResponse : class, new()
{
    protected readonly IEntityService<TDomain> Service;
    protected readonly IPayloadValidator<TRequestBody> Validator;
    protected readonly IMapper Mapper;

    protected BaseEntityController(
        IEntityService<TDomain> service,
        IPayloadValidator<TRequestBody> validator,
        IMapper mapper)
    {
        Service = service;
        Validator = validator;
        Mapper = mapper;
    }

    [HttpGet]
    public virtual async Task<Response<PagedResponse<TResponse>>> GetPagedAsync([FromQuery] QueryOptions query)
    {
        var pagedResult = await Service.GetPagedAsync(query);
        var items = Mapper.Map<IEnumerable<TResponse>>(pagedResult.Items);
        return new PagedResponse<TResponse>(pagedResult.Page, pagedResult.PageSize, pagedResult.TotalCount, items);
    }

    [HttpGet("all")]
    public virtual async Task<Response<IEnumerable<TResponse>>> GetAllAsync()
    {
        var result = await Service.GetAllAsync();
        return new Response<IEnumerable<TResponse>>(Mapper.Map<IEnumerable<TResponse>>(result));
    }

    [HttpGet("{id}")]
    public virtual async Task<Response<TResponse>> GetAsync(Guid id)
    {
        var entity = await Service.GetAsync(id);
        if (entity != null)
            return Mapper.Map<TResponse>(entity);

        return new Response<TResponse>(
            [new Error("Entity not found")],
            HttpStatusCode.NotFound);
    }

    [HttpPost]
    public virtual async Task<Response<TResponse>> CreateAsync([FromBody] TRequestBody body)
    {
        var validationResult = await Validator.ValidateAsync(body);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .Select(e => Error.ForField(e.PropertyName, e.ErrorMessage))
                .ToArray();
            return new Response<TResponse>(errors, HttpStatusCode.BadRequest);
        }

        var domain = Mapper.Map<TDomain>(body);
        domain.CreatedDate = domain.UpdatedDate = DateTime.UtcNow;

        var result = await Service.UpsertAsync(domain);
        return result is not null
            ? Mapper.Map<TResponse>(result)
            : new Response<TResponse>(
                [$"An error occurred while attempting to create the {typeof(TDomain).Name}."],
                HttpStatusCode.BadRequest);
    }

    [HttpPut("{id}")]
    public virtual async Task<Response<TResponse>> UpdateAsync(Guid id, [FromBody] TRequestBody body)
    {
        body.Id = id;

        var validationResult = await Validator.ValidateAsync(body);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .Select(e => Error.ForField(e.PropertyName, e.ErrorMessage))
                .ToArray();
            return new Response<TResponse>(errors, HttpStatusCode.BadRequest);
        }

        var existing = await Service.GetAsync(id);
        if (existing == null)
            return new Response<TResponse>([new Error("Entity not found")], HttpStatusCode.NotFound);

        var domain = Mapper.Map<TDomain>(body);
        domain.CreatedDate = existing.CreatedDate;
        domain.UpdatedDate = DateTime.UtcNow;

        var result = await Service.UpsertAsync(domain);
        return result is not null
            ? Mapper.Map<TResponse>(result)
            : new Response<TResponse>(
                [$"An error occurred while attempting to update the resource {id}."],
                HttpStatusCode.BadRequest);
    }

    [HttpDelete("{id}")]
    public virtual async Task<Response<TResponse>> DeleteAsync(Guid id)
    {
        var existing = await Service.GetAsync(id);
        if (existing == null)
            return new Response<TResponse>([new Error("Entity not found")], HttpStatusCode.NotFound);

        await Service.DeleteAsync(id);
        return new Response<TResponse>(errors: Enumerable.Empty<Error>(), statusCode: HttpStatusCode.NoContent);
    }

    [HttpGet("field/{fieldName}")]
    public async Task<Response<bool>> CheckDuplicateAsync(
        string fieldName,
        [FromQuery] string fieldValue,
        [FromQuery] Guid? excludeEntityId = null)
    {
        return await Service.CheckDuplicateAsync(fieldName, fieldValue, excludeEntityId);
    }
}
