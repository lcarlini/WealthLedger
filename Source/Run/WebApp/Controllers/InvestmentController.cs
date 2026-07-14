using System.Globalization;
using System.Net;
using System.Text;
using AutoMapper;
using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Application.Validators;
using WealthLedger.Contracts.Api.Requests;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;
using WealthLedger.WebApp.Csv;
using WealthLedger.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace WealthLedger.WebApp.Controllers;

[Route("api/investments")]
public class InvestmentController
    : BaseEntityController<Investment, InvestmentRequestBody, InvestmentResponse>
{
    private readonly IInvestmentService _investmentService;
    private readonly IRepository<FinancialInstitution> _institutionRepository;

    public InvestmentController(
        IInvestmentService service,
        IPayloadValidator<InvestmentRequestBody> validator,
        IMapper mapper,
        IRepository<FinancialInstitution> institutionRepository)
        : base(service, validator, mapper)
    {
        _investmentService = service;
        _institutionRepository = institutionRepository;
    }

    [HttpGet("export-csv")]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportCsvAsync()
    {
        var result = await Service.GetAllAsync();
        var mapped = Mapper.Map<IEnumerable<InvestmentResponse>>(result);
        var items = (await EnrichResponses(mapped)).ToList();
        var sb = new StringBuilder();
        const string sep = ",";
        sb.Append("Id").Append(sep).Append("FinancialInstitutionId").Append(sep).Append("InstitutionName").Append(sep)
            .Append("Name").Append(sep).Append("AccountType").Append(sep).Append("Currency").Append(sep)
            .Append("Amount").Append(sep).Append("CdiPercentage").Append(sep).Append("AnnualRatePercent").Append(sep)
            .Append("MaturityDate").Append(sep).Append("RequiresMonthlyMovement").Append(sep)
            .Append("MonthlyMovementAmount").Append(sep).Append("CreatedDate").Append(sep).Append("UpdatedDate")
            .AppendLine();
        foreach (var r in items)
        {
            sb.Append(EscapeCsv(r.Id.ToString())).Append(sep)
                .Append(EscapeCsv(r.FinancialInstitutionId.ToString())).Append(sep)
                .Append(EscapeCsv(r.InstitutionName)).Append(sep)
                .Append(EscapeCsv(r.Name)).Append(sep)
                .Append(EscapeCsv(r.AccountType.ToString())).Append(sep)
                .Append(EscapeCsv(r.Currency.ToString())).Append(sep)
                .Append(r.Amount.ToString("F2", CultureInfo.InvariantCulture)).Append(sep)
                .Append(r.CdiPercentage.ToString("F2", CultureInfo.InvariantCulture)).Append(sep)
                .Append(r.AnnualRatePercent.HasValue
                    ? r.AnnualRatePercent.Value.ToString("F2", CultureInfo.InvariantCulture)
                    : "").Append(sep)
                .Append(EscapeCsv(r.MaturityDate?.ToString("o", CultureInfo.InvariantCulture))).Append(sep)
                .Append(r.RequiresMonthlyMovement ? "true" : "false").Append(sep)
                .Append(r.MonthlyMovementAmount.HasValue
                    ? r.MonthlyMovementAmount.Value.ToString("F2", CultureInfo.InvariantCulture)
                    : "").Append(sep)
                .Append(EscapeCsv(r.CreatedDate.ToString("o", CultureInfo.InvariantCulture))).Append(sep)
                .Append(EscapeCsv(r.UpdatedDate.ToString("o", CultureInfo.InvariantCulture)))
                .AppendLine();
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "investments.csv");
    }

    [HttpGet("import-csv-template")]
    [Produces("text/csv")]
    public IActionResult ImportCsvTemplate()
    {
        const string header =
            "Id,FinancialInstitutionId,InstitutionName,Name,AccountType,Currency,Amount,CdiPercentage,AnnualRatePercent,MaturityDate,RequiresMonthlyMovement,MonthlyMovementAmount,CreatedDate,UpdatedDate\r\n";
        return File(Encoding.UTF8.GetBytes(header), "text/csv", "investments-template.csv");
    }

    [HttpPost("import-csv")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10_485_760)]
    public async Task<Response<CsvImportResult>> ImportCsvAsync(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return new Response<CsvImportResult>([new Error("No file uploaded.")], HttpStatusCode.BadRequest);

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken);
        var text = Encoding.UTF8.GetString(ms.ToArray());

        var parsed = WealthLedgerCsvParser.Parse(text);
        if (!parsed.Headers.Any(h => string.Equals(h, "Name", StringComparison.OrdinalIgnoreCase)))
        {
            return new Response<CsvImportResult>(
                [new Error("CSV must include a \"Name\" column.")],
                HttpStatusCode.BadRequest);
        }

        if (!parsed.Headers.Any(h => string.Equals(h, "AccountType", StringComparison.OrdinalIgnoreCase)))
        {
            return new Response<CsvImportResult>(
                [new Error("CSV must include an \"AccountType\" column.")],
                HttpStatusCode.BadRequest);
        }

        var institutions = (await _institutionRepository.GetAllAsync()).ToList();

        var result = new CsvImportResult
        {
            Skipped = parsed.SkippedBlankLines,
            RowsTotal = parsed.Rows.Count
        };
        result.RowErrors.AddRange(parsed.ParseErrors);

        foreach (var (lineNumber, cells) in parsed.Rows)
        {
            var body = TryMapInvestmentRow(cells, institutions, out var mapError);
            if (body == null)
            {
                result.Failed++;
                result.RowErrors.Add(new CsvImportRowError
                {
                    LineNumber = lineNumber,
                    Message = mapError ?? "Invalid row."
                });
                continue;
            }

            var existingInv = body.Id != Guid.Empty ? await Service.GetAsync(body.Id) : null;
            if (existingInv != null
                && existingInv.AccountType == AccountType.FixedTerm
                && body.Amount != existingInv.Amount)
            {
                result.Failed++;
                result.RowErrors.Add(new CsvImportRowError
                {
                    LineNumber = lineNumber,
                    Field = nameof(InvestmentRequestBody.Amount),
                    Message = "Cannot change amount of a fixed-term investment."
                });
                continue;
            }

            var validation = await Validator.ValidateAsync(body);
            if (!validation.IsValid)
            {
                result.Failed++;
                foreach (var err in validation.Errors)
                {
                    result.RowErrors.Add(new CsvImportRowError
                    {
                        LineNumber = lineNumber,
                        Field = err.PropertyName,
                        Message = err.ErrorMessage
                    });
                }

                continue;
            }

            var existing = body.Id != Guid.Empty ? await Service.GetAsync(body.Id) : null;
            var isCreate = existing == null;

            var domain = Mapper.Map<Investment>(body);
            if (isCreate)
                domain.CreatedDate = domain.UpdatedDate = DateTime.UtcNow;
            else
            {
                domain.CreatedDate = existing!.CreatedDate;
                domain.UpdatedDate = DateTime.UtcNow;
            }

            await Service.UpsertAsync(domain);
            if (isCreate)
                result.Created++;
            else
                result.Updated++;
        }

        return new Response<CsvImportResult>(result);
    }

    [HttpGet("by-institution/{institutionId}")]
    public async Task<Response<IEnumerable<InvestmentResponse>>> GetByInstitutionIdAsync(Guid institutionId)
    {
        var result = await _investmentService.GetByInstitutionIdAsync(institutionId);
        var mapped = Mapper.Map<IEnumerable<InvestmentResponse>>(result);
        var responses = await EnrichResponses(mapped);
        return new Response<IEnumerable<InvestmentResponse>>(responses);
    }

    public override async Task<Response<InvestmentResponse>> CreateAsync([FromBody] InvestmentRequestBody body)
    {
        var response = await base.CreateAsync(body);
        if (response.Data != null)
            await EnrichSingle(response.Data);
        return response;
    }

    public override async Task<Response<InvestmentResponse>> UpdateAsync(Guid id, [FromBody] InvestmentRequestBody body)
    {
        var existing = await Service.GetAsync(id);
        if (existing == null)
            return new Response<InvestmentResponse>([new Error("Entity not found")], HttpStatusCode.NotFound);

        if (existing.AccountType == AccountType.FixedTerm && body.Amount != existing.Amount)
            return new Response<InvestmentResponse>(
                [Error.ForField("Amount", "Cannot change amount of a fixed-term investment.")],
                HttpStatusCode.BadRequest);

        var response = await base.UpdateAsync(id, body);
        if (response.Data != null)
            await EnrichSingle(response.Data);
        return response;
    }

    public override async Task<Response<Contracts.Domain.Pagination.PagedResponse<InvestmentResponse>>> GetPagedAsync(
        [FromQuery] Contracts.Api.Queries.QueryOptions query)
    {
        var response = await base.GetPagedAsync(query);
        if (response.Data?.Items != null)
            await EnrichResponses(response.Data.Items);
        return response;
    }

    public override async Task<Response<IEnumerable<InvestmentResponse>>> GetAllAsync()
    {
        var response = await base.GetAllAsync();
        if (response.Data != null)
            await EnrichResponses(response.Data);
        return response;
    }

    public override async Task<Response<InvestmentResponse>> GetAsync(Guid id)
    {
        var response = await base.GetAsync(id);
        if (response.Data != null)
            await EnrichSingle(response.Data);
        return response;
    }

    private async Task<IEnumerable<InvestmentResponse>> EnrichResponses(IEnumerable<InvestmentResponse> responses)
    {
        var list = responses.ToList();
        foreach (var r in list)
            await EnrichSingle(r);
        return list;
    }

    private async Task EnrichSingle(InvestmentResponse r)
    {
        var inst = await _institutionRepository.GetAsync(r.FinancialInstitutionId);
        r.InstitutionName = inst?.Name;
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private static InvestmentRequestBody? TryMapInvestmentRow(
        Dictionary<string, string> cells,
        IReadOnlyList<FinancialInstitution> institutions,
        out string? error)
    {
        error = null;
        var idText = CsvRowHelper.GetCell(cells, "Id");
        _ = Guid.TryParse(idText, out var id);

        var body = new InvestmentRequestBody { Id = id };

        Guid? fiId = null;
        var fiText = CsvRowHelper.GetCell(cells, "FinancialInstitutionId");
        if (!string.IsNullOrWhiteSpace(fiText)
            && Guid.TryParse(fiText.Trim(), out var parsedFi)
            && parsedFi != Guid.Empty)
        {
            fiId = parsedFi;
        }

        if (fiId == null)
        {
            var name = CsvRowHelper.GetCell(cells, "InstitutionName")?.Trim();
            if (!string.IsNullOrEmpty(name))
            {
                var matches = institutions
                    .Where(i => string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (matches.Count == 0)
                {
                    error = $"No financial institution named '{name}'.";
                    return null;
                }

                if (matches.Count > 1)
                {
                    error = $"Multiple institutions named '{name}' — use FinancialInstitutionId.";
                    return null;
                }

                fiId = matches[0].Id;
            }
        }

        if (!fiId.HasValue || fiId.Value == Guid.Empty)
        {
            error = "FinancialInstitutionId or a unique InstitutionName is required.";
            return null;
        }

        body.FinancialInstitutionId = fiId.Value;

        body.Name = CsvRowHelper.GetCell(cells, "Name")?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(body.Name))
        {
            error = "Name is required.";
            return null;
        }

        var atText = CsvRowHelper.GetCell(cells, "AccountType");
        if (string.IsNullOrWhiteSpace(atText)
            || !Enum.TryParse<AccountType>(atText.Trim(), true, out var accountType))
        {
            error = "Invalid or missing AccountType.";
            return null;
        }

        body.AccountType = accountType;

        var curText = CsvRowHelper.GetCell(cells, "Currency") ?? "BRL";
        if (!Enum.TryParse<Currency>(curText.Trim(), true, out var currency))
        {
            error = "Invalid Currency.";
            return null;
        }

        body.Currency = currency;

        var amountText = CsvRowHelper.GetCell(cells, "Amount") ?? "0";
        if (!decimal.TryParse(amountText, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
        {
            error = "Invalid Amount.";
            return null;
        }

        body.Amount = amount;

        var cdiText = CsvRowHelper.GetCell(cells, "CdiPercentage") ?? "0";
        if (!decimal.TryParse(cdiText, NumberStyles.Any, CultureInfo.InvariantCulture, out var cdi))
        {
            error = "Invalid CdiPercentage.";
            return null;
        }

        body.CdiPercentage = cdi;

        var arpText = CsvRowHelper.GetCell(cells, "AnnualRatePercent");
        if (string.IsNullOrWhiteSpace(arpText))
            body.AnnualRatePercent = null;
        else if (decimal.TryParse(arpText, NumberStyles.Any, CultureInfo.InvariantCulture, out var arp))
            body.AnnualRatePercent = arp;
        else
        {
            error = "Invalid AnnualRatePercent.";
            return null;
        }

        var matText = CsvRowHelper.GetCell(cells, "MaturityDate");
        if (string.IsNullOrWhiteSpace(matText))
            body.MaturityDate = null;
        else if (DateTime.TryParse(
                     matText,
                     CultureInfo.InvariantCulture,
                     DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal,
                     out var mat))
            body.MaturityDate = mat.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(mat, DateTimeKind.Utc)
                : mat.ToUniversalTime();
        else
        {
            error = "Invalid MaturityDate.";
            return null;
        }

        var rmText = CsvRowHelper.GetCell(cells, "RequiresMonthlyMovement") ?? "false";
        if (!bool.TryParse(rmText.Trim(), out var reqMov))
        {
            error = "Invalid RequiresMonthlyMovement.";
            return null;
        }

        body.RequiresMonthlyMovement = reqMov;

        var mmText = CsvRowHelper.GetCell(cells, "MonthlyMovementAmount");
        if (string.IsNullOrWhiteSpace(mmText))
            body.MonthlyMovementAmount = null;
        else if (decimal.TryParse(mmText, NumberStyles.Any, CultureInfo.InvariantCulture, out var mm))
            body.MonthlyMovementAmount = mm;
        else
        {
            error = "Invalid MonthlyMovementAmount.";
            return null;
        }

        return body;
    }
}
