using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Market;

namespace WealthLedger.Infrastructure.Services;

/// <summary>
/// Tesouro Direto from the Tesouro Transparente open-data CSV (newest day first, so only the top of the file is
/// read) and benchmark indices from Yahoo Finance. Results are cached; if a refresh fails the last good data is
/// served and flagged as cached.
/// </summary>
public sealed class MarketToolsService(IHttpClientFactory httpClientFactory, ILogger<MarketToolsService> logger)
    : IMarketToolsService
{
    private const string TreasuryCsvUrl =
        "https://www.tesourotransparente.gov.br/ckan/dataset/df56aa42-484a-4a59-8184-7676580c81e3/resource/796d2059-14e9-44e3-80c9-2d9e30b405c1/download/PrecoTaxaTesouroDireto.csv";

    private static readonly TimeSpan TreasuryTtl = TimeSpan.FromHours(6);
    private static readonly TimeSpan IndicesTtl = TimeSpan.FromMinutes(15);

    private static readonly (string Symbol, string Name)[] IndexList =
    [
        ("^BVSP", "Ibovespa"),
        ("^GSPC", "S&P 500"),
        ("^IXIC", "Nasdaq"),
        ("BRL=X", "USD/BRL"),
    ];

    private static readonly object CacheLock = new();
    private static TreasurySnapshot? _treasury;
    private static MarketIndicesSnapshot? _indices;

    // ---- Tesouro Direto ----------------------------------------------------------------------

    public async Task<TreasurySnapshot> GetTreasuryAsync(CancellationToken ct = default)
    {
        var cached = Volatile.Read(ref _treasury);
        if (cached is not null && DateTime.UtcNow - cached.FetchedAtUtc < TreasuryTtl)
            return cached with { FromCache = true };

        try
        {
            using var client = CreateClient();
            using var response = await client.GetAsync(TreasuryCsvUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            // The file is published in Windows-1252; the bond names are plain ASCII so Latin-1 is exact.
            using var reader = new StreamReader(stream, System.Text.Encoding.Latin1);
            var snapshot = ParseLatestTreasury(reader, DateTime.UtcNow);

            lock (CacheLock) _treasury = snapshot;
            return snapshot;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException)
        {
            if (ct.IsCancellationRequested) throw;
            logger.LogWarning(ex, "Tesouro Direto refresh failed");
            if (cached is not null) return cached with { FromCache = true };
            throw new InvalidOperationException("Tesouro Direto data is unavailable right now. Try again later.", ex);
        }
    }

    /// <summary>
    /// Reads rows of the most recent base date. The file lists the newest day first, so reading stops as soon as the
    /// date changes. Format: <c>Tipo;Vencimento;DataBase;TaxaCompra;TaxaVenda;PUCompra;PUVenda;PUBase</c>,
    /// dd/MM/yyyy dates and decimal commas.
    /// </summary>
    public static TreasurySnapshot ParseLatestTreasury(TextReader reader, DateTime fetchedAtUtc)
    {
        var header = reader.ReadLine();
        if (header is null || !header.StartsWith("Tipo Titulo", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("Unexpected Tesouro Direto file layout.");

        var bonds = new List<TreasuryBond>();
        DateTime? baseDate = null;
        var pt = CultureInfo.GetCultureInfo("pt-BR");

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var c = line.Split(';');
            if (c.Length < 7) continue;
            if (!DateTime.TryParseExact(c[2], "dd/MM/yyyy", pt, DateTimeStyles.None, out var rowDate)) continue;

            baseDate ??= rowDate;
            if (rowDate != baseDate) break;

            if (!DateTime.TryParseExact(c[1], "dd/MM/yyyy", pt, DateTimeStyles.None, out var maturity)) continue;
            if (!decimal.TryParse(c[3], NumberStyles.Number, pt, out var buyRate)) continue;
            if (!decimal.TryParse(c[4], NumberStyles.Number, pt, out var sellRate)) continue;
            if (!decimal.TryParse(c[5], NumberStyles.Number, pt, out var buyPrice)) continue;
            if (!decimal.TryParse(c[6], NumberStyles.Number, pt, out var sellPrice)) continue;

            bonds.Add(new TreasuryBond(c[0].Trim(), maturity, buyRate, sellRate, buyPrice, sellPrice));
        }

        if (baseDate is null || bonds.Count == 0)
            throw new FormatException("The Tesouro Direto file has no rows.");

        var ordered = bonds.OrderBy(b => b.Type, StringComparer.Ordinal).ThenBy(b => b.Maturity).ToList();
        return new TreasurySnapshot(baseDate.Value, ordered, fetchedAtUtc, FromCache: false);
    }

    // ---- Indices -----------------------------------------------------------------------------

    public async Task<MarketIndicesSnapshot> GetIndicesAsync(CancellationToken ct = default)
    {
        var cached = Volatile.Read(ref _indices);
        if (cached is not null && DateTime.UtcNow - cached.FetchedAtUtc < IndicesTtl)
            return cached with { FromCache = true };

        using var client = CreateClient();
        var results = await Task.WhenAll(IndexList.Select(i => FetchIndexAsync(client, i.Symbol, i.Name, ct)));
        var indices = results.Where(r => r is not null).Select(r => r!).ToList();

        if (indices.Count == 0)
        {
            if (cached is not null) return cached with { FromCache = true };
            throw new InvalidOperationException("Market index data is unavailable right now. Try again later.");
        }

        var snapshot = new MarketIndicesSnapshot(indices, DateTime.UtcNow, FromCache: false);
        lock (CacheLock) _indices = snapshot;
        return snapshot;
    }

    private async Task<MarketIndex?> FetchIndexAsync(HttpClient client, string symbol, string name, CancellationToken ct)
    {
        try
        {
            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?range=1mo&interval=1d";
            using var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return ParseYahooChart(doc.RootElement, symbol, name);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            if (ct.IsCancellationRequested) throw;
            logger.LogWarning(ex, "Index {Symbol} refresh failed", symbol);
            return null;
        }
    }

    /// <summary>Extracts price, day change and the closing series from a Yahoo Finance chart response.</summary>
    public static MarketIndex? ParseYahooChart(JsonElement root, string symbol, string name)
    {
        var result = root.GetProperty("chart").GetProperty("result");
        if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() == 0) return null;
        var first = result[0];
        var meta = first.GetProperty("meta");

        if (!meta.TryGetProperty("regularMarketPrice", out var priceEl) || priceEl.ValueKind != JsonValueKind.Number)
            return null;
        var price = priceEl.GetDecimal();

        var closes = new List<decimal>();
        if (first.TryGetProperty("indicators", out var ind)
            && ind.TryGetProperty("quote", out var quote) && quote.GetArrayLength() > 0
            && quote[0].TryGetProperty("close", out var closeArr) && closeArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var v in closeArr.EnumerateArray())
                if (v.ValueKind == JsonValueKind.Number) closes.Add(v.GetDecimal()); // skips null (holiday) entries
        }

        decimal previous = meta.TryGetProperty("chartPreviousClose", out var pc) && pc.ValueKind == JsonValueKind.Number
            ? pc.GetDecimal()
            : 0m;
        // With a 1-month range chartPreviousClose is the close before the range, so derive the day change from the series.
        if (closes.Count >= 2) previous = closes[^2];

        var periodStart = closes.Count > 0 ? closes[0] : previous;
        var currency = meta.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "" : "";

        return new MarketIndex(
            symbol, name, currency, price,
            DayChangePercent: Percent(price, previous),
            PeriodChangePercent: Percent(price, periodStart),
            Points: closes);
    }

    private static decimal Percent(decimal now, decimal before)
        => before == 0m ? 0m : Math.Round((now - before) / before * 100m, 2);

    private HttpClient CreateClient()
    {
        var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "WealthLedger/1.0");
        return client;
    }
}
