using System.Text.Json;
using WealthLedger.Contracts.Domain.Enums;
using WealthLedger.Infrastructure.Services;
using Xunit;

namespace WealthLedger.Infrastructure.Tests;

public sealed class StockQuoteParsingTests
{
    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Parses_a_complete_brapi_result()
    {
        var item = Parse("""{ "symbol": "PETR4", "regularMarketPrice": 38.5, "currency": "BRL", "regularMarketTime": "2026-11-05T18:00:00Z" }""");

        var quote = StockQuoteService.ParseBrapiItem(item, "PETR4");

        Assert.NotNull(quote);
        Assert.Equal("PETR4", quote!.Ticker);
        Assert.Equal(38.5m, quote.Price);
        Assert.Equal("BRL", quote.Currency);
        Assert.Equal("brapi", quote.Source);
        Assert.NotNull(quote.AsOf);
    }

    [Theory]
    [InlineData("""{ "symbol": "PETR4", "regularMarketPrice": null }""")]
    [InlineData("""{ "symbol": "PETR4", "regularMarketPrice": 0 }""")]
    [InlineData("""{ "symbol": "PETR4", "regularMarketPrice": -3.2 }""")]
    [InlineData("""{ "symbol": "PETR4", "regularMarketPrice": "38.5" }""")]
    [InlineData("""{ "symbol": "PETR4" }""")]
    public void Rejects_missing_or_non_positive_prices(string json)
    {
        Assert.Null(StockQuoteService.ParseBrapiItem(Parse(json), "PETR4"));
    }

    [Fact]
    public void Falls_back_to_brl_and_the_requested_ticker_when_fields_are_absent()
    {
        var quote = StockQuoteService.ParseBrapiItem(Parse("""{ "regularMarketPrice": 10 }"""), "BOVA11");

        Assert.NotNull(quote);
        Assert.Equal("BOVA11", quote!.Ticker);
        Assert.Equal("BRL", quote.Currency);
    }

    [Fact]
    public void Non_object_payloads_yield_no_quote()
    {
        Assert.Null(StockQuoteService.ParseBrapiItem(Parse("[]"), "PETR4"));
    }

    [Theory]
    [InlineData("PETR4", true)]
    [InlineData("BOVA11", true)]
    [InlineData("HGLG11", true)]
    [InlineData("BBDC4F", true)]
    [InlineData("petr4", true)]
    [InlineData(" PETR4 ", true)]
    [InlineData("AAPL", false)]
    [InlineData("VOO", false)]
    [InlineData("BTC", false)]
    [InlineData("", false)]
    public void Recognises_b3_codes(string ticker, bool expected)
    {
        Assert.Equal(expected, StockQuoteService.IsB3Ticker(ticker));
    }

    [Fact]
    public void Unavailable_message_for_us_tickers_explains_the_limit()
    {
        var message = InvestmentService.QuoteUnavailableMessage(AccountType.Stock, "AAPL");

        Assert.Contains("not a B3 code", message);
    }

    [Fact]
    public void Unavailable_message_for_b3_tickers_suggests_retrying()
    {
        var message = InvestmentService.QuoteUnavailableMessage(AccountType.ETF, "BOVA11");

        Assert.Contains("No quote returned", message);
    }

    [Fact]
    public void Unavailable_message_for_crypto_points_to_common_symbols()
    {
        Assert.Contains("CoinGecko", InvestmentService.QuoteUnavailableMessage(AccountType.Crypto, "XYZ"));
    }
}
