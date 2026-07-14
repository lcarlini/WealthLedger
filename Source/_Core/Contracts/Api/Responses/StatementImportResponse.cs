using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Contracts.Api.Responses;

public class StatementImportResponse
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public StatementSource Source { get; set; }
    public int TransactionCount { get; set; }
    public DateTime CreatedDate { get; set; }
}
