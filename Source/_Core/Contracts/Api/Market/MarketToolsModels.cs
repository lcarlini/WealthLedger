namespace WealthLedger.Contracts.Api.Market;

/// <summary>One Tesouro Direto bond as published for the latest trading day.</summary>
/// <param name="BuyRatePercent">Annual rate when buying (for Selic bonds, the spread over Selic).</param>
/// <param name="BuyPrice">Unit price (PU) when buying, in BRL.</param>
public sealed record TreasuryBond(
    string Type,
    DateTime Maturity,
    decimal BuyRatePercent,
    decimal SellRatePercent,
    decimal BuyPrice,
    decimal SellPrice);

public sealed record TreasurySnapshot(
    DateTime BaseDate,
    IReadOnlyList<TreasuryBond> Bonds,
    DateTime FetchedAtUtc,
    bool FromCache);

/// <summary>A market index with its recent trend.</summary>
/// <param name="Points">Closing values, oldest first, for a small trend chart.</param>
public sealed record MarketIndex(
    string Symbol,
    string Name,
    string Currency,
    decimal Price,
    decimal DayChangePercent,
    decimal PeriodChangePercent,
    IReadOnlyList<decimal> Points);

public sealed record MarketIndicesSnapshot(
    IReadOnlyList<MarketIndex> Indices,
    DateTime FetchedAtUtc,
    bool FromCache);
