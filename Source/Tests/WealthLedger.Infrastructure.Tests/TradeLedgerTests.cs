using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WealthLedger.Contracts.Api.Trades;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;
using WealthLedger.Infrastructure.Data;
using WealthLedger.Infrastructure.Repositories;
using WealthLedger.Infrastructure.Services;
using Xunit;

namespace WealthLedger.Infrastructure.Tests;

public sealed class TradePositionCalculatorTests
{
    private static readonly Guid Inv = Guid.NewGuid();
    private static readonly DateTime Day1 = new(2026, 1, 1);

    private static TradeTransaction Trade(TradeSide side, decimal qty, decimal price, int dayOffset = 0, decimal fees = 0m) => new()
    {
        Id = Guid.NewGuid(),
        InvestmentId = Inv,
        Side = side,
        TradeDate = Day1.AddDays(dayOffset),
        Quantity = qty,
        UnitPrice = price,
        Fees = fees,
        CreatedDate = Day1,
        UpdatedDate = Day1,
    };

    [Fact]
    public void Weighted_average_cost_across_two_buys()
    {
        var position = TradePositionCalculator.Calculate(Inv, [Trade(TradeSide.Buy, 10, 10), Trade(TradeSide.Buy, 10, 20, 1)]);

        Assert.Equal(20m, position.Quantity);
        Assert.Equal(15m, position.AveragePrice);
        Assert.Equal(300m, position.CostBasis);
        Assert.Equal(0m, position.RealizedPnl);
    }

    [Fact]
    public void Partial_sell_realises_gain_at_average_cost_and_keeps_average()
    {
        var position = TradePositionCalculator.Calculate(Inv,
            [Trade(TradeSide.Buy, 10, 10), Trade(TradeSide.Buy, 10, 20, 1), Trade(TradeSide.Sell, 5, 30, 2)]);

        Assert.Equal(15m, position.Quantity);
        Assert.Equal(15m, position.AveragePrice);
        Assert.Equal(75m, position.RealizedPnl); // (30 - 15) * 5
        Assert.Equal(225m, position.CostBasis);
    }

    [Fact]
    public void Fees_raise_the_cost_of_buys_and_reduce_sale_proceeds()
    {
        // Buy 10 @ 10 with 5 fees: cost 105 (avg 10.50).
        // Sell 10 @ 12 with 4 fees: proceeds 116, cost removed 105, realised 11.
        var position = TradePositionCalculator.Calculate(Inv,
            [Trade(TradeSide.Buy, 10, 10, fees: 5), Trade(TradeSide.Sell, 10, 12, 1, fees: 4)]);

        Assert.Equal(0m, position.Quantity);
        Assert.Equal(0m, position.AveragePrice);
        Assert.Equal(11m, position.RealizedPnl);
    }

    [Fact]
    public void Selling_more_than_held_on_that_date_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            TradePositionCalculator.Calculate(Inv, [Trade(TradeSide.Buy, 5, 10), Trade(TradeSide.Sell, 6, 10, 1)]));
    }

    [Fact]
    public void Trades_are_applied_in_date_order_not_insertion_order()
    {
        // The sell is listed first but happens after the buy, so it is valid.
        var position = TradePositionCalculator.Calculate(Inv,
            [Trade(TradeSide.Sell, 4, 12, 3), Trade(TradeSide.Buy, 10, 10)]);

        Assert.Equal(6m, position.Quantity);
    }

    [Fact]
    public void Empty_history_is_an_empty_position()
    {
        var position = TradePositionCalculator.Calculate(Inv, []);

        Assert.Equal(0m, position.Quantity);
        Assert.Equal(0, position.TradeCount);
    }
}

public sealed class TradeLedgerServiceTests : IDisposable
{
    private readonly TempDirectory _dir = new();
    private readonly WealthLedgerDbContext _db;
    private readonly TradeLedgerService _service;
    private readonly Guid _investmentId = Guid.NewGuid();

    public TradeLedgerServiceTests()
    {
        var path = Path.Combine(_dir.Path, "ledger.db");
        _db = new WealthLedgerDbContext(
            new DbContextOptionsBuilder<WealthLedgerDbContext>().UseSqlite($"Data Source={path}").Options);
        _db.Database.EnsureCreated();

        _service = new TradeLedgerService(
            new EfRepository<TradeTransaction>(_db),
            new EfRepository<Investment>(_db),
            new UnitOfWork(_db));
    }

    public void Dispose()
    {
        _db.Dispose();
        _dir.Dispose();
    }

    /// <summary>A holding created before the ledger: quantity and average exist, but no trades.</summary>
    private async Task SeedLegacyHoldingAsync(decimal quantity = 10m, decimal average = 5m)
    {
        var now = new DateTime(2026, 1, 10);
        _db.Investments.Add(new Investment
        {
            Id = _investmentId,
            Name = "PETR4",
            AccountType = AccountType.Stock,
            Currency = Currency.BRL,
            Amount = quantity * average,
            Ticker = "PETR4",
            Quantity = quantity,
            AveragePrice = average,
            Status = InvestmentStatus.Active,
            StartDate = now,
            CreatedDate = now,
            UpdatedDate = now,
        });
        await _db.SaveChangesAsync();
    }

    private TradeRequest Sell(decimal qty, decimal price, string day = "2026-01-20") =>
        new(_investmentId, TradeSide.Sell, DateTime.Parse(day), qty, price, 0m, null);

    private TradeRequest Buy(decimal qty, decimal price, string day = "2026-01-15") =>
        new(_investmentId, TradeSide.Buy, DateTime.Parse(day), qty, price, 0m, null);

    [Fact]
    public async Task First_trade_on_a_legacy_holding_keeps_its_position_as_an_opening_balance()
    {
        await SeedLegacyHoldingAsync(quantity: 10m, average: 5m);

        await _service.AddTradeAsync(Sell(3m, 8m));

        var position = await _service.GetPositionAsync(_investmentId);
        Assert.Equal(7m, position.Quantity);
        Assert.Equal(5m, position.AveragePrice);
        Assert.Equal(9m, position.RealizedPnl); // (8 - 5) * 3

        var trades = await _service.GetTradesAsync(_investmentId);
        Assert.Equal(2, trades.Count);
        Assert.Contains(trades, t => t.Side == TradeSide.Buy && t.Quantity == 10m);
    }

    [Fact]
    public async Task Holding_quantity_and_average_price_follow_the_ledger()
    {
        await SeedLegacyHoldingAsync(quantity: 10m, average: 5m);
        await _service.AddTradeAsync(Buy(10m, 15m));

        var investment = await _db.Investments.AsNoTracking().SingleAsync(i => i.Id == _investmentId);
        Assert.Equal(20m, investment.Quantity);
        Assert.Equal(10m, investment.AveragePrice);
    }

    [Fact]
    public async Task Overselling_is_rejected_and_nothing_is_written()
    {
        await SeedLegacyHoldingAsync(quantity: 10m);

        await Assert.ThrowsAsync<ArgumentException>(() => _service.AddTradeAsync(Sell(11m, 8m)));

        Assert.Empty(await _service.GetTradesAsync(_investmentId));
        var investment = await _db.Investments.AsNoTracking().SingleAsync(i => i.Id == _investmentId);
        Assert.Equal(10m, investment.Quantity);
    }

    [Fact]
    public async Task Deleting_a_buy_that_later_sales_depend_on_is_refused()
    {
        await SeedLegacyHoldingAsync(quantity: 0m, average: 0m);
        var buy = await _service.AddTradeAsync(Buy(10m, 10m));
        await _service.AddTradeAsync(Sell(10m, 12m));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.DeleteTradeAsync(buy.Id));

        Assert.Equal(2, (await _service.GetTradesAsync(_investmentId)).Count);
    }

    [Fact]
    public async Task Deleting_the_latest_trade_restores_the_previous_position()
    {
        await SeedLegacyHoldingAsync(quantity: 10m, average: 5m);
        var sell = await _service.AddTradeAsync(Sell(4m, 9m));

        await _service.DeleteTradeAsync(sell.Id);

        var position = await _service.GetPositionAsync(_investmentId);
        Assert.Equal(10m, position.Quantity);
        Assert.Equal(5m, position.AveragePrice);
    }

    [Fact]
    public async Task Fixed_income_holdings_do_not_accept_trades()
    {
        var now = new DateTime(2026, 1, 10);
        _db.Investments.Add(new Investment
        {
            Id = _investmentId, Name = "CDB", AccountType = AccountType.FixedTerm, Currency = Currency.BRL,
            Status = InvestmentStatus.Active, StartDate = now, CreatedDate = now, UpdatedDate = now,
        });
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _service.AddTradeAsync(Buy(1m, 100m)));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(5, -1)]
    public async Task Invalid_quantity_or_price_is_rejected(decimal qty, decimal price)
    {
        await SeedLegacyHoldingAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => _service.AddTradeAsync(Buy(qty, price)));
    }
}

public sealed class YahooQuoteParsingTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Parses_a_yahoo_chart_quote()
    {
        var root = Parse("""
            {"chart":{"result":[{"meta":{"currency":"USD","symbol":"VOO","regularMarketPrice":673.97,"regularMarketTime":1793912400}}],"error":null}}
            """);

        var quote = StockQuoteService.ParseYahooChart(root, "VOO");

        Assert.NotNull(quote);
        Assert.Equal("VOO", quote!.Ticker);
        Assert.Equal(673.97m, quote.Price);
        Assert.Equal("USD", quote.Currency);
        Assert.Equal("yahoo", quote.Source);
        Assert.NotNull(quote.AsOf);
    }

    [Fact]
    public void Brazilian_suffixed_symbol_is_reported_under_the_requested_ticker()
    {
        var root = Parse("""{"chart":{"result":[{"meta":{"currency":"BRL","regularMarketPrice":45.95}}]}}""");

        var quote = StockQuoteService.ParseYahooChart(root, "PETR4");

        Assert.Equal("PETR4", quote!.Ticker);
        Assert.Equal("BRL", quote.Currency);
    }

    [Fact]
    public void Unknown_symbol_returns_no_quote()
    {
        var root = Parse("""{"chart":{"result":null,"error":{"code":"Not Found"}}}""");

        Assert.Null(StockQuoteService.ParseYahooChart(root, "ZZZZZZ"));
    }

    [Theory]
    [InlineData("""{"chart":{"result":[{"meta":{"regularMarketPrice":0,"currency":"USD"}}]}}""")]
    [InlineData("""{"chart":{"result":[{"meta":{"currency":"USD"}}]}}""")]
    [InlineData("""{"chart":{"result":[{"meta":{"regularMarketPrice":"12.3"}}]}}""")]
    public void Rejects_missing_or_invalid_prices(string json)
    {
        Assert.Null(StockQuoteService.ParseYahooChart(Parse(json), "AAPL"));
    }
}
