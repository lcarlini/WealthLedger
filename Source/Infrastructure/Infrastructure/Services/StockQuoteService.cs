using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WealthLedger.Infrastructure.Services;

/// <summary>
/// Price sources, all free and keyless unless configured otherwise:
/// - B3 codes (PETR4, BOVA11, ...): brapi.dev, falling back to Yahoo with the ".SA" suffix.
/// - Other equities and ETFs (AAPL, VOO, ...): Yahoo Finance chart endpoint.
/// - Crypto: CoinGecko.
/// Yahoo is an unofficial endpoint and can change or rate-limit without notice; it can be turned off with
/// StockQuotes:YahooEnabled = false.
/// </summary>
public class StockQuoteService : IStockQuoteService
{
    private static readonly TimeSpan QuoteCacheTtl = TimeSpan.FromMinutes(15);
    private static readonly ConcurrentDictionary<string, CachedQuote> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>brapi accepts several tickers per request; keep batches small to stay within URL limits.</summary>
    private const int BrapiBatchSize = 20;

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
        if (TryGetCached(normalized, accountType, out var fresh))
            return fresh;

        try
        {
            var client = CreateClient();

            StockQuote? quote = accountType == AccountType.Crypto
                ? await FetchCryptoQuoteAsync(client, normalized, cancellationToken)
                : await FetchEquityQuoteAsync(client, normalized, cancellationToken);

            if (quote != null)
                Remember(normalized, accountType, quote);

            return quote;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Quote fetch failed for {Ticker} ({AccountType})", normalized, accountType);
            return Cache.TryGetValue(CacheKey(normalized, accountType), out var stale) ? stale.Quote : null;
        }
    }

    public async Task<IReadOnlyDictionary<string, StockQuote>> GetQuotesAsync(
        IEnumerable<(string Ticker, AccountType AccountType)> requests,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, StockQuote>(StringComparer.OrdinalIgnoreCase);
        var pending = new List<(string Ticker, AccountType Type)>();

        foreach (var key in requests
                     .Select(r => (Ticker: NormalizeTicker(r.Ticker ?? string.Empty), Type: r.AccountType))
                     .Where(k => k.Ticker.Length > 0)
                     .Distinct())
        {
            if (TryGetCached(key.Ticker, key.Type, out var cached))
                result[key.Ticker] = cached!;
            else
                pending.Add(key);
        }

        // One brapi request per batch instead of one per holding. Needs a token: brapi refuses
        // multi-ticker requests without one, so without a token each ticker is fetched on its own.
        if (!string.IsNullOrWhiteSpace(_configuration["StockQuotes:BrapiToken"]))
        {
            var batchable = pending.Where(p => p.Type != AccountType.Crypto && IsB3Ticker(p.Ticker)).ToList();
            foreach (var chunk in batchable.Chunk(BrapiBatchSize))
            {
                var batch = await FetchBrapiBatchAsync(chunk.Select(c => c.Ticker).ToList(), cancellationToken);
                foreach (var (ticker, type) in chunk)
                {
                    if (batch.TryGetValue(ticker, out var quote))
                    {
                        result[ticker] = quote;
                        Remember(ticker, type, quote);
                    }
                }
            }
        }

        // Anything not yet priced (no batch, or missing from the batch) goes through the single-ticker path.
        foreach (var (ticker, type) in pending.Where(p => !result.ContainsKey(p.Ticker)).ToList())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var quote = await GetQuoteAsync(ticker, type, cancellationToken);
            if (quote != null)
                result[ticker] = quote;
        }

        return result;
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "WealthLedger/1.0");
        client.Timeout = TimeSpan.FromSeconds(20);
        return client;
    }

    private async Task<StockQuote?> FetchEquityQuoteAsync(
        HttpClient client,
        string ticker,
        CancellationToken cancellationToken)
    {
        if (IsB3Ticker(ticker))
        {
            var brapi = await FetchBrapiQuoteAsync(client, ticker, cancellationToken);
            if (brapi != null)
                return brapi;

            return _configuration.GetValue("StockQuotes:YahooEnabled", true)
                ? await FetchYahooQuoteAsync(client, ticker + ".SA", ticker, cancellationToken)
                : null;
        }

        return _configuration.GetValue("StockQuotes:YahooEnabled", true)
            ? await FetchYahooQuoteAsync(client, ticker, ticker, cancellationToken)
            : null;
    }

    private async Task<StockQuote?> FetchBrapiQuoteAsync(
        HttpClient client,
        string ticker,
        CancellationToken cancellationToken)
    {
        var url = BrapiUrl(Uri.EscapeDataString(ticker));

        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("brapi returned {Status} for {Ticker}", (int)response.StatusCode, ticker);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!doc.RootElement.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() == 0)
            return null;

        var quote = ParseBrapiItem(results[0], ticker);
        if (quote != null)
            _logger.LogInformation("brapi {Ticker}: {Price} {Currency}", quote.Ticker, quote.Price, quote.Currency);

        return quote;
    }

    private async Task<Dictionary<string, StockQuote>> FetchBrapiBatchAsync(
        IReadOnlyList<string> tickers,
        CancellationToken cancellationToken)
    {
        var quotes = new Dictionary<string, StockQuote>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var client = CreateClient();
            var list = string.Join(",", tickers.Select(Uri.EscapeDataString));

            using var response = await client.GetAsync(BrapiUrl(list), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("brapi batch returned {Status} for {Count} tickers", (int)response.StatusCode, tickers.Count);
                return quotes;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                return quotes;

            foreach (var item in results.EnumerateArray())
            {
                var symbol = item.TryGetProperty("symbol", out var sym) && sym.ValueKind == JsonValueKind.String
                    ? sym.GetString()
                    : null;
                if (string.IsNullOrEmpty(symbol))
                    continue;

                var quote = ParseBrapiItem(item, symbol);
                if (quote != null)
                    quotes[NormalizeTicker(symbol)] = quote;
            }

            _logger.LogInformation("brapi batch priced {Priced} of {Count} tickers", quotes.Count, tickers.Count);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "brapi batch request failed; falling back to single-ticker requests");
        }

        return quotes;
    }

    private string BrapiUrl(string tickerList)
    {
        var token = _configuration["StockQuotes:BrapiToken"];
        return string.IsNullOrWhiteSpace(token)
            ? $"https://brapi.dev/api/quote/{tickerList}"
            : $"https://brapi.dev/api/quote/{tickerList}?token={Uri.EscapeDataString(token)}";
    }

    private async Task<StockQuote?> FetchYahooQuoteAsync(
        HttpClient client,
        string symbol,
        string requestedTicker,
        CancellationToken cancellationToken)
    {
        var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?range=1d&interval=1d";

        using var response = await client.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Yahoo returned {Status} for {Symbol}", (int)response.StatusCode, symbol);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var quote = ParseYahooChart(doc.RootElement, requestedTicker);
        if (quote != null)
            _logger.LogInformation("Yahoo {Symbol}: {Price} {Currency}", symbol, quote.Price, quote.Currency);

        return quote;
    }

    /// <summary>Parses one brapi quote result. Returns null when there is no usable positive price.</summary>
    public static StockQuote? ParseBrapiItem(JsonElement item, string requestedTicker)
    {
        if (item.ValueKind != JsonValueKind.Object)
            return null;

        if (!item.TryGetProperty("regularMarketPrice", out var priceEl) ||
            priceEl.ValueKind != JsonValueKind.Number ||
            !priceEl.TryGetDecimal(out var price) ||
            price <= 0)
            return null;

        var currency = item.TryGetProperty("currency", out var cur) && cur.ValueKind == JsonValueKind.String
            ? cur.GetString() ?? "BRL"
            : "BRL";

        DateTime? asOf = null;
        if (item.TryGetProperty("regularMarketTime", out var timeEl) &&
            timeEl.ValueKind == JsonValueKind.String &&
            DateTime.TryParse(timeEl.GetString(), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var parsed))
        {
            asOf = parsed.ToUniversalTime();
        }

        var symbol = item.TryGetProperty("symbol", out var sym) && sym.ValueKind == JsonValueKind.String
            ? sym.GetString() ?? requestedTicker
            : requestedTicker;

        return new StockQuote
        {
            Ticker = symbol,
            Price = price,
            Currency = currency.ToUpperInvariant(),
            AsOf = asOf,
            Source = "brapi"
        };
    }

    /// <summary>Parses a Yahoo Finance chart response. Unknown symbols come back with a null result.</summary>
    public static StockQuote? ParseYahooChart(JsonElement root, string requestedTicker)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("chart", out var chart) ||
            chart.ValueKind != JsonValueKind.Object ||
            !chart.TryGetProperty("result", out var results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() == 0)
            return null;

        if (!results[0].TryGetProperty("meta", out var meta) || meta.ValueKind != JsonValueKind.Object)
            return null;

        if (!meta.TryGetProperty("regularMarketPrice", out var priceEl) ||
            priceEl.ValueKind != JsonValueKind.Number ||
            !priceEl.TryGetDecimal(out var price) ||
            price <= 0)
            return null;

        var currency = meta.TryGetProperty("currency", out var cur) && cur.ValueKind == JsonValueKind.String
            ? cur.GetString() ?? "USD"
            : "USD";

        DateTime? asOf = null;
        if (meta.TryGetProperty("regularMarketTime", out var timeEl) &&
            timeEl.ValueKind == JsonValueKind.Number &&
            timeEl.TryGetInt64(out var seconds))
        {
            asOf = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
        }

        return new StockQuote
        {
            Ticker = requestedTicker,
            Price = price,
            Currency = currency.ToUpperInvariant(),
            AsOf = asOf,
            Source = "yahoo"
        };
    }

    /// <summary>
    /// True for B3 codes such as PETR4, BOVA11, HGLG11 or BBDC4F. US-style symbols (AAPL, VOO) are not B3 codes.
    /// </summary>
    public static bool IsB3Ticker(string ticker) =>
        Regex.IsMatch(NormalizeTicker(ticker), @"^[A-Z]{4}\d{1,2}[A-Z]?$");

    public static string NormalizeTicker(string ticker) =>
        ticker.Trim().ToUpperInvariant().Replace(" ", "", StringComparison.Ordinal);

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

    private static string CacheKey(string ticker, AccountType type) => $"{type}:{ticker}";

    private static bool TryGetCached(string ticker, AccountType type, out StockQuote? quote)
    {
        if (Cache.TryGetValue(CacheKey(ticker, type), out var cached) && cached.ExpiresAt > DateTime.UtcNow)
        {
            quote = cached.Quote;
            return true;
        }

        quote = null;
        return false;
    }

    private static void Remember(string ticker, AccountType type, StockQuote quote) =>
        Cache[CacheKey(ticker, type)] = new CachedQuote(quote, DateTime.UtcNow.Add(QuoteCacheTtl));

    private sealed record CachedQuote(StockQuote Quote, DateTime ExpiresAt);
}
