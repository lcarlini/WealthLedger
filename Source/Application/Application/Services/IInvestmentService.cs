using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Application.Services;

public interface IInvestmentService : IEntityService<Investment>
{
    Task<IEnumerable<Investment>> GetByInstitutionIdAsync(Guid institutionId);

    /// <summary>
    /// Fetches live quotes for variable-income holdings that have a ticker and quantity,
    /// then updates Amount = Quantity × price (with FX conversion when needed).
    /// </summary>
    Task<RefreshPricesResult> RefreshPricesAsync(CancellationToken cancellationToken = default);

    Task<IEnumerable<Investment>> GetByStatusAsync(InvestmentStatus status);
}
