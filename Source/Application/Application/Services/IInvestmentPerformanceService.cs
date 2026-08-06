using WealthLedger.Contracts.Api.Requests;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Application.Services;

public interface IInvestmentPerformanceService
{
    Task<InvestmentPerformanceResponse?> GetPerformanceAsync(Guid investmentId, CancellationToken ct = default);
    Task<Investment?> ArchiveAsync(Guid id, ArchiveInvestmentRequest request);
    Task<Investment?> RestoreAsync(Guid id);
}
