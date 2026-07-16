using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WealthLedger.Infrastructure.Services;

public class StockQuoteService : IStockQuoteService
{
    private static readonly TimeSpan QuoteCacheTtl = TimeSpan.FromMinutes(15);
    private static readonly ConcurrentDictionary<string, CachedQuote> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Common crypto tickers → CoinGecko coin ids.</summary>
    private static readonly Dictionary<string, string> CryptoIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BTC"] = "bitcoin",
        ["BITCOIN"] = "bitcoin",
        ["ETH"] = "ethereum",
        ["ETHEREUM"] = "ethereum",
        ["SOL"] = "solana",
        ["SOLANA"] = "solana",
        ["ADA"] = "cardano",
        ["XRP"] = "ripple",
        ["DOGE"] = "dogecoin",
        ["DOT"] = "polkadot",
        ["AVAX"] = "avalanche-2",
        ["MATIC"] = "matic-network",
        ["POL"] = "matic-network",
        ["LINK"] = "chainlink",
        ["BNB"] = "binancecoin",
        ["USDT"] = "tether",
        ["USDC"] = "usd-coin",
        ["LTC"] = "litecoin",
        ["BCH"] = "bitcoin-cash",
        ["ATOM"] = "cosmos",
        ["UNI"] = "uniswap",
        ["NEAR"] = "near",
        ["APT"] = "aptos",
        ["ARB"] = "arbitrum",
        ["OP"] = "optimism",
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StockQuoteService> _logger;

    public StockQuoteService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<StockQuoteService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<StockQuote?> GetQuoteAsync(
        string ticker,
        AccountType accountType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return null;

        var normalized = NormalizeTicker(ticker);
        var cacheKey = $"{accountType}:{normalized}";

        if (Cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
            return cached.Quote;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "WealthLedger/1.0");
            client.Timeout = TimeSpan.FromSeconds(20);

            StockQuote? quote = accountType == AccountType.Crypto
                ? await FetchCryptoQuoteAsync(client, normalized, cancellationToken)
                : await FetchBrapiQuoteAsync(client, normalized, cancellationToken);

            if (quote != null)
                Cache[cacheKey] = new CachedQuote(quote, DateTime.UtcNow.Add(QuoteCacheTtl));

            return quote;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Quote fetch failed for {Ticker} ({AccountType})", normalized, accountType);
            return Cache.TryGetValue(cacheKey, out var stale) ? stale.Quote : null;
        }
    }

    public async Task<IReadOnlyDictionary<string, StockQuote>> GetQuotesAsync(
        IEnumerable<(string Ticker, AccountType AccountType)> requests,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, StockQuote>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in requests.GroupBy(r => (NormalizeTicker(r.Ticker), r.AccountType)))
        {
            var (ticker, type) = group.Key;
            if (string.IsNullOrEmpty(ticker))
                continue;

            var quote = await GetQuoteAsync(ticker, type, cancellationToken);
            if (quote != null)
                result[ticker] = quote;
        }

        return result;
    }

    private async Task<StockQuote?> FetchBrapiQuoteAsync(
        HttpClient client,
        string ticker,
        CancellationToken cancellationToken)
    {
        var token = _configuration["StockQuotes:BrapiToken"];
        var url = string.IsNullOrWhiteSpace(token)
            ? $"https://brapi.dev/api/quote/{Uri.EscapeDataString(ticker)}"
            : $"https://brapi.dev/api/quote/{Uri.EscapeDataString(ticker)}?token={Uri.EscapeDataString(token)}";

        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("brapi returned {Status} for {Ticker}", (int)response.StatusCode, ticker);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            return null;

        var item = results[0];
        if (!item.TryGetProperty("regularMarketPrice", out var priceEl) ||
            priceEl.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        var price = priceEl.GetDecimal();
        if (price <= 0)
            return null;

        var currency = item.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "BRL" : "BRL";
        DateTime? asOf = null;
        if (item.TryGetProperty("regularMarketTime", out var timeEl) &&
            timeEl.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(timeEl.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var parsed))
        {
            asOf = parsed.ToUniversalTime();
        }

        var symbol = item.TryGetProperty("symbol", out var sym) ? sym.GetString() ?? ticker : ticker;

        _logger.LogInformation("brapi {Ticker}: {Price} {Currency}", symbol, price, currency);

        return new StockQuote
        {
            Ticker = symbol,
            Price = price,
            Currency = currency.ToUpperInvariant(),
            AsOf = asOf,
            Source = "brapi"
        };
    }

    private async Task<StockQuote?> FetchCryptoQuoteAsync(
        HttpClient client,
        string ticker,
        CancellationToken cancellationToken)
    {
        var coinId = ResolveCryptoId(ticker);
        var url =
            $"https://api.coingecko.com/api/v3/simple/price?ids={Uri.EscapeDataString(coinId)}&vs_currencies=usd,brl";

        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("CoinGecko returned {Status} for {CoinId}", (int)response.StatusCode, coinId);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!doc.RootElement.TryGetProperty(coinId, out var coin))
            return null;

        // Prefer BRL when available so BRL holdings need no FX conversion.
        if (coin.TryGetProperty("brl", out var brl) && brl.GetDecimal() > 0)
        {
            var price = brl.GetDecimal();
            _logger.LogInformation("CoinGecko {CoinId}: {Price} BRL", coinId, price);
            return new StockQuote
            {
                Ticker = ticker,
                Price = price,
                Currency = "BRL",
                AsOf = DateTime.UtcNow,
                Source = "coingecko"
            };
        }

        if (coin.TryGetProperty("usd", out var usd) && usd.GetDecimal() > 0)
        {
            var price = usd.GetDecimal();
            _logger.LogInformation("CoinGecko {CoinId}: {Price} USD", coinId, price);
            return new StockQuote
            {
                Ticker = ticker,
                Price = price,
                Currency = "USD",
                AsOf = DateTime.UtcNow,
                Source = "coingecko"
            };
        }

        return null;
    }

    private static string ResolveCryptoId(string ticker)
    {
        if (CryptoIds.TryGetValue(ticker, out var id))
            return id;

        // Allow users to store the CoinGecko id directly (e.g. "bitcoin", "avalanche-2").
        return ticker.ToLowerInvariant();
    }

    private static string NormalizeTicker(string ticker) =>
        ticker.Trim().ToUpperInvariant().Replace(" ", "", StringComparison.Ordinal);

    private sealed record CachedQuote(StockQuote Quote, DateTime ExpiresAt);
}
