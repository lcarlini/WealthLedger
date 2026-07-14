namespace WealthLedger.Contracts.Api.Responses;

public class CsvImportResult
{
    public int RowsTotal { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<CsvImportRowError> RowErrors { get; set; } = new();
}
