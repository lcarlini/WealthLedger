using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WealthLedger.Contracts.Domain.Enums;
using WealthLedger.Infrastructure.Services;
using Xunit;

namespace WealthLedger.Infrastructure.Tests;

/// <summary>
/// Calls the real quote providers (brapi, Yahoo, CoinGecko). Runs only when WL_LIVE_TESTS=1, so normal
/// test runs stay offline and deterministic. When the variable is not set, the test returns without asserting.
/// </summary>
public sealed class LiveQuoteTests
{
    [Fact]
    public async Task Live_refresh_prices_b3_us_and_crypto_holdings()
    {
        if (Environment.GetEnvironmentVariable("WL_LIVE_TESTS") != "1")
            return;

        var factory = new ServiceCollection().AddHttpClient().BuildServiceProvider()
            .GetRequiredService<IHttpClientFactory>();
        var service = new StockQuoteService(factory, new ConfigurationBuilder().Build(), NullLogger<StockQuoteService>.Instance);

        var quotes = await service.GetQuotesAsync(new[]
        {
            ("PETR4", AccountType.Stock),
            ("VOO", AccountType.ETF),
            ("AAPL", AccountType.Stock),
            ("BTC", AccountType.Crypto),
        });

        Assert.True(quotes.ContainsKey("PETR4"), "PETR4 (B3) was not priced");
        Assert.Equal("BRL", quotes["PETR4"].Currency);
        Assert.True(quotes.ContainsKey("VOO"), "VOO (US ETF) was not priced");
        Assert.Equal("USD", quotes["VOO"].Currency);
        Assert.True(quotes.ContainsKey("AAPL"), "AAPL (US stock) was not priced");
        Assert.True(quotes.ContainsKey("BTC"), "BTC (crypto) was not priced");
        Assert.All(quotes.Values, q => Assert.True(q.Price > 0));
    }

    /// <summary>
    /// Full refresh path: stored holdings are priced, USD quotes are converted with live PTAX, and every
    /// holding's book amount is updated. Uses a temporary SQLite database, so no real data is touched.
    /// </summary>
    [Fact]
    public async Task Live_refresh_prices_updates_holdings_with_fx_conversion()
    {
        if (Environment.GetEnvironmentVariable("WL_LIVE_TESTS") != "1")
            return;

        using var dir = new TempDirectory();
        using var db = new WealthLedger.Infrastructure.Data.WealthLedgerDbContext(
            new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<WealthLedger.Infrastructure.Data.WealthLedgerDbContext>()
                .UseSqlite($"Data Source={Path.Combine(dir.Path, "live.db")}").Options);
        db.Database.EnsureCreated();

        var now = DateTime.UtcNow;
        var holdings = new[]
        {
            Holding("PETR4", AccountType.Stock, WealthLedger.Contracts.Domain.Enums.Currency.BRL, 10m, now),
            Holding("AAPL", AccountType.Stock, WealthLedger.Contracts.Domain.Enums.Currency.BRL, 2m, now),
            Holding("VOO", AccountType.ETF, WealthLedger.Contracts.Domain.Enums.Currency.BRL, 1m, now),
        };
        db.Investments.AddRange(holdings);
        await db.SaveChangesAsync();

        var factory = new ServiceCollection().AddHttpClient().BuildServiceProvider()
            .GetRequiredService<IHttpClientFactory>();
        var quotes = new StockQuoteService(factory, new ConfigurationBuilder().Build(), NullLogger<StockQuoteService>.Instance);
        var market = new WealthLedger.Infrastructure.Services.MarketDataService(factory, NullLogger<WealthLedger.Infrastructure.Services.MarketDataService>.Instance);
        var investments = new WealthLedger.Infrastructure.Services.InvestmentService(
            new WealthLedger.Infrastructure.Repositories.EfRepository<WealthLedger.Contracts.Domain.Investment>(db),
            quotes,
            market,
            NullLogger<WealthLedger.Infrastructure.Services.InvestmentService>.Instance);

        var result = await investments.RefreshPricesAsync();

        Assert.True(result.Updated >= 2, $"Expected at least PETR4 and VOO to update. Items: {string.Join("; ", result.Items.Select(i => $"{i.Ticker}={i.Status}:{i.Message}"))}");

        var petr = await db.Investments.AsNoTracking().SingleAsync(i => i.Ticker == "PETR4");
        Assert.True(petr.Amount > 0, "PETR4 was not repriced");

        var voo = await db.Investments.AsNoTracking().SingleAsync(i => i.Ticker == "VOO");
        Assert.True(voo.Amount > 0, "VOO was not repriced");
        Assert.True(voo.Amount > 100m, "VOO converted to BRL should be far above 100");
    }

    private static WealthLedger.Contracts.Domain.Investment Holding(
        string ticker,
        AccountType type,
        WealthLedger.Contracts.Domain.Enums.Currency currency,
        decimal quantity,
        DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        FinancialInstitutionId = Guid.NewGuid(),
        Name = ticker,
        AccountType = type,
        Currency = currency,
        Ticker = ticker,
        Quantity = quantity,
        AveragePrice = 1m,
        Amount = quantity,
        PrincipalAmount = quantity,
        Status = WealthLedger.Contracts.Domain.Enums.InvestmentStatus.Active,
        StartDate = now,
        CreatedDate = now,
        UpdatedDate = now,
    };
}
