using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Trades;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Infrastructure.Services;

public class TradeLedgerService(
    IRepository<TradeTransaction> trades,
    IRepository<Investment> investments,
    IUnitOfWork unitOfWork) : ITradeLedgerService
{
    public async Task<IReadOnlyList<TradeResponse>> GetTradesAsync(Guid investmentId, CancellationToken ct = default)
    {
        return (await LoadTradesAsync(investmentId))
            .OrderBy(t => t.TradeDate)
            .ThenBy(t => t.CreatedDate)
            .Select(Map)
            .ToList();
    }

    public async Task<PositionSummary> GetPositionAsync(
        Guid investmentId,
        CostMethod method = CostMethod.WeightedAverage,
        CancellationToken ct = default)
    {
        return TradePositionCalculator.Calculate(investmentId, await LoadTradesAsync(investmentId), method);
    }

    public async Task<TradePerformance> GetPerformanceAsync(
        Guid investmentId,
        CostMethod method = CostMethod.WeightedAverage,
        CancellationToken ct = default)
    {
        var investment = await investments.GetAsync(investmentId)
                         ?? throw new ArgumentException("Investment not found.");
        var ordered = (await LoadTradesAsync(investmentId))
            .OrderBy(t => t.TradeDate)
            .ThenBy(t => t.CreatedDate)
            .ThenBy(t => t.Id)
            .ToList();
        var detail = TradePositionCalculator.CalculateDetailed(investmentId, ordered, method);

        // Money in is negative, money out and the closing value are positive.
        var flows = new List<(DateTime Date, double Amount)>();
        foreach (var trade in ordered)
        {
            switch (trade.Side)
            {
                case TradeSide.Buy:
                    flows.Add((trade.TradeDate, -(double)(trade.Quantity * trade.UnitPrice + trade.Fees)));
                    break;
                case TradeSide.Sell:
                    flows.Add((trade.TradeDate, (double)(trade.Quantity * trade.UnitPrice - trade.Fees)));
                    break;
            }
        }

        foreach (var dividend in detail.Dividends)
            flows.Add((dividend.Date, (double)dividend.Amount));

        // Market value comes from the stored Amount, which is the market value once prices are refreshed
        // and the cost basis until then.
        var marketValue = detail.Summary.Quantity > 0m ? investment.Amount : 0m;
        if (marketValue > 0m)
            flows.Add((DateTime.UtcNow.Date, (double)marketValue));

        var rate = Xirr.Compute(flows);
        var unrealized = marketValue - detail.Summary.CostBasis;

        return new TradePerformance(
            InvestmentId: investmentId,
            Method: method.ToString(),
            MarketValue: marketValue,
            CostBasis: detail.Summary.CostBasis,
            RealizedPnl: detail.Summary.RealizedPnl,
            DividendIncome: detail.Summary.DividendIncome,
            UnrealizedPnl: marketValue > 0m ? Math.Round(unrealized, 2, MidpointRounding.AwayFromZero) : 0m,
            XirrAnnualPercent: rate is null ? null : Math.Round((decimal)(rate.Value * 100), 2, MidpointRounding.AwayFromZero));
    }

    public async Task<TaxReport> GetTaxReportAsync(int year, CancellationToken ct = default)
    {
        var holdings = (await investments.GetAllAsync()).ToDictionary(i => i.Id);
        var taxable = new List<TaxableSale>();

        foreach (var group in (await trades.GetAllAsync()).GroupBy(t => t.InvestmentId))
        {
            if (!holdings.TryGetValue(group.Key, out var holding))
                continue;

            var category = CategoryOf(holding.AccountType);
            var detail = TradePositionCalculator.CalculateDetailed(group.Key, group);
            foreach (var sale in detail.Sales)
                taxable.Add(new TaxableSale(category, sale.Date, sale.Proceeds, sale.Gain));
        }

        // Losses from earlier years still offset gains later, so the whole history is computed and then filtered.
        var prefix = year.ToString("0000") + "-";
        var months = SalesTaxCalculator.Compute(taxable)
            .Where(m => m.Month.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();

        return new TaxReport(year, SalesTaxCalculator.Disclaimer, months, months.Sum(m => m.TaxDue));
    }

    public async Task<TradeResponse> AddTradeAsync(TradeRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request);

        var investment = await investments.GetAsync(request.InvestmentId)
                         ?? throw new ArgumentException("Investment not found.");
        if (!AccountTypeRules.IsVariableIncome(investment.AccountType))
            throw new ArgumentException("Trades are only supported for variable-income holdings (stocks, ETFs, FIIs, BDRs, funds and crypto).");

        var existing = (await LoadTradesAsync(request.InvestmentId)).ToList();
        var now = DateTime.UtcNow;
        var toAdd = new List<TradeTransaction>();

        // Holdings created before the ledger carry a position but no trades. Keep that position by
        // recording it as an opening balance, so the first real trade does not erase it.
        if (existing.Count == 0 && (investment.Quantity ?? 0m) > 0m)
        {
            var anchor = investment.StartDate.Date;
            var openingDate = (anchor < request.TradeDate.Date ? anchor : request.TradeDate.Date).AddDays(-1);
            toAdd.Add(new TradeTransaction
            {
                Id = Guid.NewGuid(),
                InvestmentId = investment.Id,
                Side = TradeSide.Buy,
                TradeDate = openingDate,
                Quantity = investment.Quantity!.Value,
                UnitPrice = investment.AveragePrice ?? 0m,
                Fees = 0m,
                Notes = "Opening balance (position recorded before the trade ledger existed)",
                CreatedDate = now,
                UpdatedDate = now,
            });
        }

        var entity = new TradeTransaction
        {
            Id = Guid.NewGuid(),
            InvestmentId = investment.Id,
            Side = request.Side,
            TradeDate = request.TradeDate.Date,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            Fees = request.Fees,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            CreatedDate = now,
            UpdatedDate = now,
        };
        toAdd.Add(entity);

        // Validate the whole history, including the new trades, before anything is written.
        var position = TradePositionCalculator.Calculate(investment.Id, existing.Concat(toAdd));

        await unitOfWork.ExecuteAsync(async () =>
        {
            foreach (var trade in toAdd)
                await trades.UpsertAsync(trade);
            await SyncInvestmentAsync(investment, position);
        }, ct);

        return Map(entity);
    }

    public async Task DeleteTradeAsync(Guid tradeId, CancellationToken ct = default)
    {
        var trade = await trades.GetAsync(tradeId) ?? throw new ArgumentException("Trade not found.");
        var remaining = (await LoadTradesAsync(trade.InvestmentId)).Where(t => t.Id != tradeId).ToList();

        PositionSummary position;
        try
        {
            position = TradePositionCalculator.Calculate(trade.InvestmentId, remaining);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                $"This trade cannot be removed because later sales depend on it. {ex.Message}");
        }

        await unitOfWork.ExecuteAsync(async () =>
        {
            await trades.DeleteAsync(tradeId);
            var investment = await investments.GetAsync(trade.InvestmentId);
            if (investment != null)
                await SyncInvestmentAsync(investment, position);
        }, ct);
    }

    private async Task<List<TradeTransaction>> LoadTradesAsync(Guid investmentId) =>
        (await trades.GetAllAsync()).Where(t => t.InvestmentId == investmentId).ToList();

    /// <summary>Quantity and average price follow the ledger. Amount becomes the cost basis until prices are refreshed.</summary>
    private async Task SyncInvestmentAsync(Investment investment, PositionSummary position)
    {
        investment.Quantity = position.Quantity;
        investment.AveragePrice = position.Quantity > 0 ? position.AveragePrice : null;
        investment.Amount = position.CostBasis;
        investment.UpdatedDate = DateTime.UtcNow;
        await investments.UpsertAsync(investment);
    }

    private static TaxCategory CategoryOf(AccountType type) => type switch
    {
        AccountType.Stock => TaxCategory.Stock,
        AccountType.FII => TaxCategory.Fii,
        AccountType.ETF => TaxCategory.Etf,
        AccountType.BDR => TaxCategory.Bdr,
        AccountType.Crypto => TaxCategory.Crypto,
        _ => TaxCategory.Other,
    };

    private static void ValidateRequest(TradeRequest request)
    {
        if (!Enum.IsDefined(request.Side))
            throw new ArgumentException("Unknown trade type.");
        if (request.Fees < 0)
            throw new ArgumentException("Fees cannot be negative.");

        switch (request.Side)
        {
            case TradeSide.Buy:
            case TradeSide.Sell:
                if (request.Quantity <= 0)
                    throw new ArgumentException("Quantity must be greater than zero.");
                if (request.UnitPrice < 0)
                    throw new ArgumentException("Unit price cannot be negative.");
                break;

            case TradeSide.Dividend:
                // Dividends are per share: the amount goes in UnitPrice and no shares are traded.
                if (request.Quantity != 0)
                    throw new ArgumentException("For a dividend, leave the quantity empty and enter the amount per share.");
                if (request.UnitPrice <= 0)
                    throw new ArgumentException("Dividend amount per share must be greater than zero.");
                if (request.Fees != 0)
                    throw new ArgumentException("Dividends do not have fees.");
                break;

            case TradeSide.Bonus:
                if (request.Quantity <= 0)
                    throw new ArgumentException("Bonus shares must be greater than zero.");
                if (request.UnitPrice != 0)
                    throw new ArgumentException("Bonus shares are received at no cost; leave the price at zero.");
                if (request.Fees != 0)
                    throw new ArgumentException("Bonus shares do not have fees.");
                break;
        }
    }

    private static TradeResponse Map(TradeTransaction t) => new(
        t.Id, t.InvestmentId, t.Side, t.TradeDate, t.Quantity, t.UnitPrice, t.Fees, t.Notes);
}
