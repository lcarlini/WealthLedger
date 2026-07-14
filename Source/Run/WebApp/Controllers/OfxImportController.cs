using System.Net;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;
using WealthLedger.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace WealthLedger.WebApp.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/import")]
public class OfxImportController : ControllerBase
{
    private readonly IOfxImportService _importService;

    public OfxImportController(IOfxImportService importService)
    {
        _importService = importService;
    }

    [HttpPost("ofx")]
    [RequestSizeLimit(5_242_880)]
    public async Task<Response<StatementImportResponse>> UploadOfxAsync(
        IFormFile file,
        [FromQuery] StatementSource source = StatementSource.Checking)
    {
        if (file == null || file.Length == 0)
            return new Response<StatementImportResponse>([new Error("No file uploaded")], HttpStatusCode.BadRequest);

        using var stream = file.OpenReadStream();
        var importEntity = await _importService.ImportOfxAsync(stream, file.FileName, source);
        var response = new StatementImportResponse
        {
            Id = importEntity.Id,
            FileName = importEntity.FileName,
            Source = importEntity.Source,
            TransactionCount = importEntity.TransactionCount,
            CreatedDate = importEntity.CreatedDate
        };
        return response;
    }

    [HttpGet("ofx")]
    public async Task<Response<IEnumerable<StatementImportResponse>>> GetImportsAsync()
    {
        var list = await _importService.GetImportsAsync();
        var response = list.Select(i => new StatementImportResponse
        {
            Id = i.Id,
            FileName = i.FileName,
            Source = i.Source,
            TransactionCount = i.TransactionCount,
            CreatedDate = i.CreatedDate
        }).ToList();
        return new Response<IEnumerable<StatementImportResponse>>(response);
    }

    [HttpGet("ofx/{importId}/transactions")]
    public async Task<Response<IEnumerable<BankTransactionResponse>>> GetTransactionsAsync(Guid importId)
    {
        var list = await _importService.GetTransactionsByImportIdAsync(importId);
        var response = list.Select(t => new BankTransactionResponse
        {
            Id = t.Id,
            StatementImportId = t.StatementImportId,
            TransactionType = t.TransactionType,
            Date = t.Date,
            Amount = t.Amount,
            Description = t.Description,
            Category = t.Category,
            InstallmentNumber = t.InstallmentNumber,
            InstallmentTotal = t.InstallmentTotal
        }).ToList();
        return new Response<IEnumerable<BankTransactionResponse>>(response);
    }

    [HttpDelete("ofx/{importId}")]
    public async Task<Response<object>> DeleteImportAsync(Guid importId)
    {
        var deleted = await _importService.DeleteImportAsync(importId);
        if (!deleted)
            return new Response<object>([new Error("Import not found")], HttpStatusCode.NotFound);
        return new Response<object>(errors: [], statusCode: HttpStatusCode.NoContent);
    }
}
