using WealthLedger.Contracts.Api.Market;

namespace WealthLedger.Application.Services;

/// <summary>Public market information: Tesouro Direto prices and rates, and benchmark index trends.</summary>
public interface IMarketToolsService
{
    /// <exception cref="InvalidOperationException">The source is unreachable and nothing is cached.</exception>
    Task<TreasurySnapshot> GetTreasuryAsync(CancellationToken ct = default);

    /// <exception cref="InvalidOperationException">The source is unreachable and nothing is cached.</exception>
    Task<MarketIndicesSnapshot> GetIndicesAsync(CancellationToken ct = default);
}
