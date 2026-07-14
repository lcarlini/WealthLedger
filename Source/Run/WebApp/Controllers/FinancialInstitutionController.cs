using System.Globalization;
using System.Net;
using System.Text;
using AutoMapper;
using WealthLedger.Application.Services;
using WealthLedger.Application.Validators;
using WealthLedger.Contracts.Api.Requests;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;
using WealthLedger.WebApp.Csv;
using WealthLedger.WebApp.Models;
using Microsoft.AspNetCore.Mvc;

namespace WealthLedger.WebApp.Controllers;

[Route("api/financial-institutions")]
public class FinancialInstitutionController
    : BaseEntityController<FinancialInstitution, FinancialInstitutionRequestBody, FinancialInstitutionResponse>
{
    public FinancialInstitutionController(
        IEntityService<FinancialInstitution> service,
        IPayloadValidator<FinancialInstitutionRequestBody> validator,
        IMapper mapper)
        : base(service, validator, mapper)
    {
    }

    [HttpGet("export-csv")]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportCsvAsync()
    {
        var rows = await Service.GetAllAsync();
        var items = Mapper.Map<IEnumerable<FinancialInstitutionResponse>>(rows).ToList();
        var sb = new StringBuilder();
        const string sep = ",";
        sb.Append("Id").Append(sep).Append("Name").Append(sep).Append("Description").Append(sep)
            .Append("ImageUrl").Append(sep).Append("CreatedDate").Append(sep).Append("UpdatedDate")
            .AppendLine();
        foreach (var r in items)
        {
            sb.Append(EscapeCsv(r.Id.ToString())).Append(sep)
                .Append(EscapeCsv(r.Name)).Append(sep)
                .Append(EscapeCsv(r.Description)).Append(sep)
                .Append(EscapeCsv(r.ImageUrl)).Append(sep)
                .Append(EscapeCsv(r.CreatedDate.ToString("o", CultureInfo.InvariantCulture))).Append(sep)
                .Append(EscapeCsv(r.UpdatedDate.ToString("o", CultureInfo.InvariantCulture)))
                .AppendLine();
        }

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "financial-institutions.csv");
    }

    [HttpGet("import-csv-template")]
    [Produces("text/csv")]
    public IActionResult ImportCsvTemplate()
    {
        const string header = "Id,Name,Description,ImageUrl,CreatedDate,UpdatedDate\r\n";
        return File(Encoding.UTF8.GetBytes(header), "text/csv", "financial-institutions-template.csv");
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

        var result = new CsvImportResult
        {
            Skipped = parsed.SkippedBlankLines,
            RowsTotal = parsed.Rows.Count
        };
        result.RowErrors.AddRange(parsed.ParseErrors);

        foreach (var (lineNumber, cells) in parsed.Rows)
        {
            var body = MapFinancialInstitutionRow(cells);
            if (string.IsNullOrWhiteSpace(body.Name))
            {
                result.Failed++;
                result.RowErrors.Add(new CsvImportRowError
                {
                    LineNumber = lineNumber,
                    Field = nameof(FinancialInstitutionRequestBody.Name),
                    Message = "Name is required."
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

            var domain = Mapper.Map<FinancialInstitution>(body);
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

    private static FinancialInstitutionRequestBody MapFinancialInstitutionRow(Dictionary<string, string> cells)
    {
        var idText = CsvRowHelper.GetCell(cells, "Id");
        _ = Guid.TryParse(idText, out var id);

        return new FinancialInstitutionRequestBody
        {
            Id = id,
            Name = CsvRowHelper.GetCell(cells, "Name")?.Trim() ?? string.Empty,
            Description = NullIfEmpty(CsvRowHelper.GetCell(cells, "Description")),
            ImageUrl = NullIfEmpty(CsvRowHelper.GetCell(cells, "ImageUrl"))
        };
    }

    private static string? NullIfEmpty(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
