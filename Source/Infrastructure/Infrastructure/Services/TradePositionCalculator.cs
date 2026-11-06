using WealthLedger.Contracts.Api.Trades;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Infrastructure.Services;

/// <summary>One sale: what it brought in, what cost it released, and the resulting gain or loss.</summary>
public sealed record SaleEvent(DateTime Date, decimal Quantity, decimal Proceeds, decimal CostRemoved, decimal Gain);

/// <summary>Cash dividend received on the shares held at the date.</summary>
public sealed record DividendEvent(DateTime Date, decimal Amount);

public sealed record PositionDetail(
    PositionSummary Summary,
    IReadOnlyList<SaleEvent> Sales,
    IReadOnlyList<DividendEvent> Dividends);

/// <summary>
/// Position derived from a trade history. Pure and side-effect free so it can be tested with fixed inputs.
/// Fees increase the cost of buys and reduce sale proceeds. Bonus shares add quantity at zero cost.
/// </summary>
public static class TradePositionCalculator
{
    public static PositionSummary Calculate(
        Guid investmentId,
        IEnumerable<TradeTransaction> trades,
        CostMethod method = CostMethod.WeightedAverage) =>
        CalculateDetailed(investmentId, trades, method).Summary;

    public static PositionDetail CalculateDetailed(
        Guid investmentId,
        IEnumerable<TradeTransaction> trades,
        CostMethod method = CostMethod.WeightedAverage)
    {
        var ordered = trades
            .OrderBy(t => t.TradeDate)
            .ThenBy(t => t.CreatedDate)
            .ThenBy(t => t.Id)
            .ToList();

        var lots = new List<Lot>();
        var sales = new List<SaleEvent>();
        var dividends = new List<DividendEvent>();

        foreach (var trade in ordered)
        {
            switch (trade.Side)
            {
                case TradeSide.Buy:
                    AddLot(lots, method, trade.Quantity, trade.Quantity * trade.UnitPrice + trade.Fees);
                    break;

                case TradeSide.Bonus:
                    AddLot(lots, method, trade.Quantity, 0m);
                    break;

                case TradeSide.Dividend:
                    var shares = Held(lots);
                    if (shares > 0m)
                        dividends.Add(new DividendEvent(trade.TradeDate, shares * trade.UnitPrice));
                    break;

                case TradeSide.Sell:
                    var held = Held(lots);
                    if (trade.Quantity > held)
                    {
                        throw new ArgumentException(
                            $"Selling {trade.Quantity} on {trade.TradeDate:yyyy-MM-dd} exceeds the {held} held at that date.");
                    }

                    var costRemoved = RemoveQuantity(lots, trade.Quantity);
                    var proceeds = trade.Quantity * trade.UnitPrice - trade.Fees;
                    sales.Add(new SaleEvent(trade.TradeDate, trade.Quantity, proceeds, costRemoved, proceeds - costRemoved));
                    break;

                default:
                    throw new ArgumentException($"Unsupported trade side {trade.Side}.");
            }
        }

        var quantity = Held(lots);
        var costBasis = lots.Sum(l => l.Cost);
        if (quantity == 0m)
            costBasis = 0m;

        var average = quantity > 0m ? costBasis / quantity : 0m;

        var summary = new PositionSummary(
            InvestmentId: investmentId,
            Quantity: quantity,
            AveragePrice: Math.Round(average, 6, MidpointRounding.AwayFromZero),
            CostBasis: Math.Round(costBasis, 2, MidpointRounding.AwayFromZero),
            RealizedPnl: Math.Round(sales.Sum(s => s.Gain), 2, MidpointRounding.AwayFromZero),
            TradeCount: ordered.Count,
            DividendIncome: Math.Round(dividends.Sum(d => d.Amount), 2, MidpointRounding.AwayFromZero));

        return new PositionDetail(summary, sales, dividends);
    }

    private sealed class Lot
    {
        public Lot(decimal quantity, decimal cost)
        {
            Quantity = quantity;
            Cost = cost;
        }

        public decimal Quantity { get; set; }
        public decimal Cost { get; set; }
    }

    private static decimal Held(List<Lot> lots) => lots.Sum(l => l.Quantity);

    private static void AddLot(List<Lot> lots, CostMethod method, decimal quantity, decimal cost)
    {
        if (method == CostMethod.Fifo)
        {
            lots.Add(new Lot(quantity, cost));
            return;
        }

        // Weighted average keeps one merged lot.
        if (lots.Count == 0)
            lots.Add(new Lot(quantity, cost));
        else
        {
            lots[0].Quantity += quantity;
            lots[0].Cost += cost;
        }
    }

    /// <summary>
    /// Removes shares and returns the cost they carried. Weighted average has one merged lot; FIFO walks
    /// the lots oldest first. Both use the same walk, so only the lot layout differs.
    /// </summary>
    private static decimal RemoveQuantity(List<Lot> lots, decimal quantity)
    {
        var remaining = quantity;
        decimal removed = 0m;

        for (var i = 0; i < lots.Count && remaining > 0m; i++)
        {
            var lot = lots[i];
            if (lot.Quantity <= 0m)
                continue;

            var take = Math.Min(remaining, lot.Quantity);
            var part = lot.Cost * take / lot.Quantity;

            lot.Quantity -= take;
            lot.Cost -= part;
            removed += part;
            remaining -= take;

            if (lot.Quantity == 0m)
                lot.Cost = 0m;
        }

        lots.RemoveAll(l => l.Quantity == 0m);
        return removed;
    }
}
