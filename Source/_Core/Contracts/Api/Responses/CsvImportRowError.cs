namespace WealthLedger.Contracts.Api.Responses;

public class CsvImportRowError
{
    public int LineNumber { get; set; }
    public string? Field { get; set; }
    public string Message { get; set; } = string.Empty;
}
