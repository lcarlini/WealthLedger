using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Contracts.Domain;

public class StatementImport : BaseEntity
{
    public string FileName { get; set; } = string.Empty;
    public StatementSource Source { get; set; }
    public int TransactionCount { get; set; }
}
