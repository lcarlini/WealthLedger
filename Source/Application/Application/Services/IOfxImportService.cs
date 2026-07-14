using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Application.Services;

public interface IOfxImportService
{
    Task<StatementImport> ImportOfxAsync(Stream fileContent, string fileName, StatementSource source);
    Task<IEnumerable<StatementImport>> GetImportsAsync();
    Task<IEnumerable<BankTransaction>> GetTransactionsByImportIdAsync(Guid importId);
    Task<bool> DeleteImportAsync(Guid importId);
    Task<int> RecategorizeAllAsync();
}
