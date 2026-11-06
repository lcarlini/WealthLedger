using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Contracts.Api.Trades;

public enum CostMethod
{
    /// <summary>Brazilian custo médio: one average cost per holding. Used for the stored position and tax.</summary>
    WeightedAverage = 1,

    /// <summary>First-in, first-out. Comparison view only; not the Brazilian tax basis.</summary>
    Fifo = 2,
}

public sealed record TradeRequest(
    Guid InvestmentId,
    TradeSide Side,
    DateTime TradeDate,
    decimal Quantity,
    decimal UnitPrice,
    decimal Fees,
    string? Notes);

public sealed record TradeResponse(
    Guid Id,
    Guid InvestmentId,
    TradeSide Side,
    DateTime TradeDate,
    decimal Quantity,
    decimal UnitPrice,
    decimal Fees,
    string? Notes);

/// <summary>Position derived from the trade history.</summary>
public sealed record PositionSummary(
    Guid InvestmentId,
    decimal Quantity,
    decimal AveragePrice,
    decimal CostBasis,
    decimal RealizedPnl,
    int TradeCount,
    decimal DividendIncome = 0m);

/// <summary>Performance of one holding. XIRR is annualised; null when it cannot be solved.</summary>
public sealed record TradePerformance(
    Guid InvestmentId,
    string Method,
    decimal MarketValue,
    decimal CostBasis,
    decimal RealizedPnl,
    decimal DividendIncome,
    decimal UnrealizedPnl,
    decimal? XirrAnnualPercent);

/// <summary>Sales-tax result for one asset category in one month.</summary>
public sealed record TaxMonth(
    string Category,
    string Month,
    decimal SalesVolume,
    decimal NetGain,
    decimal LossCarriedIn,
    decimal LossCarriedOut,
    bool ExemptByThreshold,
    decimal TaxableBase,
    decimal? RatePercent,
    decimal TaxDue,
    DateOnly? DarfDueDate);

public sealed record TaxReport(
    int Year,
    string Disclaimer,
    IReadOnlyList<TaxMonth> Months,
    decimal TotalTaxDue);
