using WealthLedger.Contracts.Api.Trades;

namespace WealthLedger.Application.Services;

/// <summary>
/// Records buys, sells, dividends and bonus shares, and keeps the holding's quantity and average cost in step.
/// Invalid operations (for example selling more than is held on that date) are reported with
/// <see cref="ArgumentException"/>, and a conflicting delete with <see cref="InvalidOperationException"/>.
/// </summary>
public interface ITradeLedgerService
{
    Task<IReadOnlyList<TradeResponse>> GetTradesAsync(Guid investmentId, CancellationToken ct = default);

    Task<PositionSummary> GetPositionAsync(Guid investmentId, CostMethod method = CostMethod.WeightedAverage, CancellationToken ct = default);

    Task<TradePerformance> GetPerformanceAsync(Guid investmentId, CostMethod method = CostMethod.WeightedAverage, CancellationToken ct = default);

    Task<TaxReport> GetTaxReportAsync(int year, CancellationToken ct = default);

    Task<TradeResponse> AddTradeAsync(TradeRequest request, CancellationToken ct = default);

    Task DeleteTradeAsync(Guid tradeId, CancellationToken ct = default);
}
