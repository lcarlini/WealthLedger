using WealthLedger.Contracts.Api.Responses;

namespace WealthLedger.Application.Services;

public interface IInvestmentService : IEntityService<Contracts.Domain.Investment>
{
    Task<IEnumerable<Contracts.Domain.Investment>> GetByInstitutionIdAsync(Guid institutionId);

    /// <summary>
    /// Fetches live quotes for variable-income holdings that have a ticker and quantity,
    /// then updates Amount = Quantity × price (with FX conversion when needed).
    /// </summary>
    Task<RefreshPricesResult> RefreshPricesAsync(CancellationToken cancellationToken = default);
}
