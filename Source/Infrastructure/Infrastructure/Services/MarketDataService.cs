using System.Text.Json;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Responses;
using Microsoft.Extensions.Logging;

namespace WealthLedger.Infrastructure.Services;

public class MarketDataService : IMarketDataService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MarketDataService> _logger;

    private static readonly TimeSpan FxCacheTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan IndicesCacheTtl = TimeSpan.FromHours(24);

    private static MarketDataCache _cache = new();

    public MarketDataService(IHttpClientFactory httpClientFactory, ILogger<MarketDataService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<MarketDataResponse> GetMarketDataAsync()
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "WealthLedger/1.0");

        var now = DateTime.UtcNow;
        var fxFromCache = false;
        var indicesFromCache = false;

        decimal usd = _cache.LastUsdBrl ?? 0;
        decimal eur = _cache.LastEurBrl ?? 0;
        DateTime? fxUpdated = _cache.LastUpdatedFx;

        if (_cache.NeedFxRefresh(now))
        {
            try
            {
                var (u, e, dt) = await FetchFxAsync(client);
                if (u > 0) { _cache.SetFx(u, e, dt); usd = u; eur = e; fxUpdated = dt; }
                else { fxFromCache = _cache.LastUsdBrl.HasValue; usd = _cache.LastUsdBrl ?? 0; eur = _cache.LastEurBrl ?? 0; fxUpdated = _cache.LastUpdatedFx; }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FX fetch failed, using cache");
                fxFromCache = true;
                usd = _cache.LastUsdBrl ?? 0;
                eur = _cache.LastEurBrl ?? 0;
                fxUpdated = _cache.LastUpdatedFx;
            }
        }
        else
        {
            fxFromCache = true;
            usd = _cache.LastUsdBrl ?? 0;
            eur = _cache.LastEurBrl ?? 0;
            fxUpdated = _cache.LastUpdatedFx;
        }

        decimal? selic = _cache.LastSelic;
        decimal? ipca = _cache.LastIpca;
        decimal? poupanca = _cache.LastPoupanca;
        DateTime? indicesUpdated = _cache.LastUpdatedIndices;

        if (_cache.NeedIndicesRefresh(now))
        {
            try
            {
                var (s, i, p, dt) = await FetchIndicesAsync(client);
                if (s.HasValue) { _cache.SetIndices(s, i, p, dt); selic = s; ipca = i; poupanca = p; indicesUpdated = dt; }
                else { indicesFromCache = _cache.LastSelic.HasValue; selic = _cache.LastSelic; ipca = _cache.LastIpca; poupanca = _cache.LastPoupanca; indicesUpdated = _cache.LastUpdatedIndices; }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Indices fetch failed, using cache");
                indicesFromCache = true;
                selic = _cache.LastSelic;
                ipca = _cache.LastIpca;
                poupanca = _cache.LastPoupanca;
                indicesUpdated = _cache.LastUpdatedIndices;
            }
        }
        else
        {
            indicesFromCache = true;
        }

        // ── Bitcoin + Fed ──
        decimal? btcUsd = _cache.LastBtcUsd;
        decimal? fedRate = _cache.LastFedFundsRate;
        if (_cache.NeedCryptoRefresh(now))
        {
            try
            {
                var (b, f) = await FetchCryptoAndFedAsync(client);
                _cache.SetCrypto(b, f);
                btcUsd = b ?? _cache.LastBtcUsd;
                fedRate = f ?? _cache.LastFedFundsRate;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "BTC/Fed fetch failed, using cache");
            }
        }

        return new MarketDataResponse
        {
            UsdBrl = usd,
            EurBrl = eur,
            SelicPercentPerYear = selic,
            IpcaPercentPerYear = ipca,
            PoupancaPercentPerYear = poupanca,
            BtcUsd = btcUsd,
            FedFundsRate = fedRate,
            LastUpdatedFx = fxUpdated,
            LastUpdatedIndices = indicesUpdated,
            FxFromCache = fxFromCache,
            IndicesFromCache = indicesFromCache
        };
    }

    private async Task<(decimal usd, decimal eur, DateTime updated)> FetchFxAsync(HttpClient client)
    {
        var date = DateTime.UtcNow;
        for (int i = 0; i < 7; i++)
        {
            var dateStr = date.ToString("MM-dd-yyyy");
            var usdUrl = $"https://olinda.bcb.gov.br/olinda/servico/PTAX/versao/v1/odata/CotacaoDolarDia(dataCotacao=@dataCotacao)?@dataCotacao='{dateStr}'&$format=json";
            var eurUrl = $"https://olinda.bcb.gov.br/olinda/servico/PTAX/versao/v1/odata/CotacaoMoedaDia(moeda=@moeda,dataCotacao=@dataCotacao)?@moeda='EUR'&@dataCotacao='{dateStr}'&$format=json";

            try
            {
                var usdRes = await client.GetStringAsync(usdUrl);
                var usdDoc = JsonDocument.Parse(usdRes);
                decimal usd = 0;
                if (usdDoc.RootElement.TryGetProperty("value", out var val) && val.GetArrayLength() > 0)
                {
                    var first = val[0];
                    if (first.TryGetProperty("cotacaoVenda", out var cv))
                        usd = cv.GetDecimal();
                }

                var eurRes = await client.GetStringAsync(eurUrl);
                var eurDoc = JsonDocument.Parse(eurRes);
                decimal eur = 0;
                if (eurDoc.RootElement.TryGetProperty("value", out var ev))
                {
                    foreach (var item in ev.EnumerateArray())
                    {
                        if (item.TryGetProperty("tipoBoletim", out var tb) && tb.GetString() == "Fechamento PTAX")
                        {
                            if (item.TryGetProperty("cotacaoVenda", out var ecv))
                                eur = ecv.GetDecimal();
                            break;
                        }
                        if (item.TryGetProperty("cotacaoVenda", out var ecv2))
                            eur = ecv2.GetDecimal();
                    }
                }

                if (usd > 0 && eur > 0)
                    return (usd, eur, date);
            }
            catch { /* try previous day */ }

            date = date.AddDays(-1);
        }

        return (0, 0, DateTime.UtcNow);
    }

    /// <summary>
    /// Fetches actual Selic Meta, IPCA 12-month accumulated, and TR from the
    /// BCB SGS (Sistema Gerenciador de Séries Temporais) API, then computes
    /// the annualised Poupança yield.
    /// </summary>
    private async Task<(decimal? selic, decimal? ipca, decimal? poupanca, DateTime? updated)> FetchIndicesAsync(HttpClient client)
    {
        decimal? selic = null;
        decimal? ipca = null;
        decimal? tr = null;

        try
        {
            // ── Selic Meta (target rate set by COPOM) ── series 432
            selic = await FetchSgsSeriesValueAsync(client, 432);
            _logger.LogInformation("BCB SGS Selic Meta: {Selic}", selic);

            // ── IPCA acumulado em 12 meses ── series 13522
            ipca = await FetchSgsSeriesValueAsync(client, 13522);
            _logger.LogInformation("BCB SGS IPCA 12m: {Ipca}", ipca);

            // ── TR (Taxa Referencial) mensal ── series 226
            tr = await FetchSgsSeriesValueAsync(client, 226);
            _logger.LogInformation("BCB SGS TR mensal: {Tr}", tr);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Indices fetch failed");
            return (null, null, null, null);
        }

        // ── Poupança (annualised) ──
        decimal? poupanca = null;
        if (selic.HasValue)
        {
            if (selic.Value > 8.5m)
            {
                // Rule: 0.5% a.m. + TR
                var trMonthly = tr ?? 0m;
                var monthlyRate = 0.5m + trMonthly; // e.g. 0.5 + 0.1695 = 0.6695%
                poupanca = Math.Round(
                    ((decimal)Math.Pow((double)(1m + monthlyRate / 100m), 12) - 1m) * 100m, 2);
            }
            else
            {
                // Rule: 70% of Selic + TR
                var selicMonthly = ((decimal)Math.Pow((double)(1m + selic.Value / 100m), 1.0 / 12.0) - 1m) * 100m;
                var trMonthly = tr ?? 0m;
                var monthlyRate = selicMonthly * 0.7m + trMonthly;
                poupanca = Math.Round(
                    ((decimal)Math.Pow((double)(1m + monthlyRate / 100m), 12) - 1m) * 100m, 2);
            }
        }

        return (selic, ipca, poupanca, DateTime.UtcNow);
    }

    /// <summary>
    /// Reads the latest value from a BCB SGS time series.
    /// URL pattern: https://api.bcb.gov.br/dados/serie/bcdata.sgs.{id}/dados/ultimos/1?formato=json
    /// Response:    [{"data":"dd/mm/yyyy","valor":"X.XX"}]
    /// </summary>
    private async Task<decimal?> FetchSgsSeriesValueAsync(HttpClient client, int seriesId)
    {
        var url = $"https://api.bcb.gov.br/dados/serie/bcdata.sgs.{seriesId}/dados/ultimos/1?formato=json";
        var json = await client.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
        {
            var valorStr = root[0].GetProperty("valor").GetString();
            if (!string.IsNullOrEmpty(valorStr) &&
                decimal.TryParse(valorStr.Replace(",", "."),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// Fetches Bitcoin price from CoinGecko and US Fed Funds Rate from FRED (St. Louis Fed).
    /// </summary>
    private async Task<(decimal? btcUsd, decimal? fedRate)> FetchCryptoAndFedAsync(HttpClient client)
    {
        decimal? btcUsd = null;
        decimal? fedRate = null;

        try
        {
            // Bitcoin price from CoinGecko (no API key needed)
            var btcJson = await client.GetStringAsync(
                "https://api.coingecko.com/api/v3/simple/price?ids=bitcoin&vs_currencies=usd");
            using var btcDoc = JsonDocument.Parse(btcJson);
            if (btcDoc.RootElement.TryGetProperty("bitcoin", out var btcObj) &&
                btcObj.TryGetProperty("usd", out var btcVal))
            {
                btcUsd = btcVal.GetDecimal();
            }
            _logger.LogInformation("CoinGecko BTC/USD: {BtcUsd}", btcUsd);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BTC price fetch failed");
        }

        try
        {
            // Fed Funds effective rate from BCB SGS series 3795 (US interest rate - a proxy)
            // Alternatively use a simplified approach: fetch from a public JSON endpoint
            // Using the Fed's public data via FRED API (no key for basic access)
            var fredUrl = "https://api.stlouisfed.org/fred/series/observations?series_id=FEDFUNDS&sort_order=desc&limit=1&file_type=json&api_key=DEMO_KEY";
            var fedJson = await client.GetStringAsync(fredUrl);
            using var fedDoc = JsonDocument.Parse(fedJson);
            if (fedDoc.RootElement.TryGetProperty("observations", out var obs) &&
                obs.GetArrayLength() > 0)
            {
                var valStr = obs[0].GetProperty("value").GetString();
                if (!string.IsNullOrEmpty(valStr) && valStr != "." &&
                    decimal.TryParse(valStr, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var val))
                {
                    fedRate = val;
                }
            }
            _logger.LogInformation("FRED Fed Funds Rate: {FedRate}", fedRate);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fed Funds rate fetch failed");
        }

        return (btcUsd, fedRate);
    }

    private class MarketDataCache
    {
        private decimal? _lastUsdBrl;
        private decimal? _lastEurBrl;
        private DateTime? _lastUpdatedFx;
        private decimal? _lastSelic;
        private decimal? _lastIpca;
        private decimal? _lastPoupanca;
        private DateTime? _lastUpdatedIndices;
        private decimal? _lastBtcUsd;
        private decimal? _lastFedFundsRate;
        private DateTime? _lastUpdatedCrypto;
        private readonly object _lock = new();

        public decimal? LastUsdBrl => _lastUsdBrl;
        public decimal? LastEurBrl => _lastEurBrl;
        public DateTime? LastUpdatedFx => _lastUpdatedFx;
        public decimal? LastSelic => _lastSelic;
        public decimal? LastIpca => _lastIpca;
        public decimal? LastPoupanca => _lastPoupanca;
        public DateTime? LastUpdatedIndices => _lastUpdatedIndices;
        public decimal? LastBtcUsd => _lastBtcUsd;
        public decimal? LastFedFundsRate => _lastFedFundsRate;

        public bool NeedFxRefresh(DateTime now)
        {
            lock (_lock)
                return !_lastUpdatedFx.HasValue || (now - _lastUpdatedFx.Value) > FxCacheTtl;
        }

        public bool NeedIndicesRefresh(DateTime now)
        {
            lock (_lock)
                return !_lastUpdatedIndices.HasValue || (now - _lastUpdatedIndices.Value) > IndicesCacheTtl;
        }

        public bool NeedCryptoRefresh(DateTime now)
        {
            lock (_lock)
                return !_lastUpdatedCrypto.HasValue || (now - _lastUpdatedCrypto.Value) > FxCacheTtl;
        }

        public void SetFx(decimal usd, decimal eur, DateTime updated)
        {
            lock (_lock)
            {
                _lastUsdBrl = usd;
                _lastEurBrl = eur;
                _lastUpdatedFx = updated;
            }
        }

        public void SetIndices(decimal? selic, decimal? ipca, decimal? poupanca, DateTime? updated)
        {
            lock (_lock)
            {
                if (selic.HasValue) _lastSelic = selic;
                if (ipca.HasValue) _lastIpca = ipca;
                if (poupanca.HasValue) _lastPoupanca = poupanca;
                if (updated.HasValue) _lastUpdatedIndices = updated;
            }
        }

        public void SetCrypto(decimal? btcUsd, decimal? fedRate)
        {
            lock (_lock)
            {
                if (btcUsd.HasValue) _lastBtcUsd = btcUsd;
                if (fedRate.HasValue) _lastFedFundsRate = fedRate;
                _lastUpdatedCrypto = DateTime.UtcNow;
            }
        }
    }
}
