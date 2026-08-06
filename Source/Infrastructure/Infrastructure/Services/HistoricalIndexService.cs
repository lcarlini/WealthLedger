using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Responses;

namespace WealthLedger.Infrastructure.Services;

/// <summary>
/// Fetches historical CDI (BCB SGS 11 = % a.d.) and IPCA for performance windows.
/// </summary>
public interface IHistoricalIndexService
{
    Task<HistoricalIndexWindow> GetWindowAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);
}

public class HistoricalIndexWindow
{
    public decimal CdiAccumulatedFactor { get; set; } = 1m;
    public decimal CdiAccumulatedPercent { get; set; }
    public decimal IpcaAccumulatedFactor { get; set; } = 1m;
    public decimal IpcaAccumulatedPercent { get; set; }
    public int CdiBusinessDays { get; set; }
    public bool UsedFallbackRates { get; set; }
    public string? Notes { get; set; }
}

public class HistoricalIndexService : IHistoricalIndexService
{
    /// <summary>BCB SGS 11 — Taxa CDI % ao dia (ex.: 0.055131 = 0,055131% a.d.).</summary>
    private const int CdiDailySeries = 11;

    /// <summary>BCB SGS 433 — IPCA variação mensal %.</summary>
    private const int IpcaMonthlySeries = 433;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMarketDataService _marketDataService;
    private readonly ILogger<HistoricalIndexService> _logger;
    private readonly Dictionary<string, HistoricalIndexWindow> _cache = new();
    private readonly object _lock = new();

    public HistoricalIndexService(
        IHttpClientFactory httpClientFactory,
        IMarketDataService marketDataService,
        ILogger<HistoricalIndexService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _marketDataService = marketDataService;
        _logger = logger;
    }

    public async Task<HistoricalIndexWindow> GetWindowAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        var start = startDate.Date;
        var end = endDate.Date;
        if (end < start)
            (start, end) = (end, start);

        var key = $"{start:yyyy-MM-dd}|{end:yyyy-MM-dd}";
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached))
                return cached;
        }

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(20);

        try
        {
            var cdiPoints = await FetchSeriesAsync(client, CdiDailySeries, start, end, ct);
            var ipcaPoints = await FetchSeriesAsync(client, IpcaMonthlySeries, start.AddMonths(-1), end, ct);

            var window = BuildWindow(start, end, cdiPoints, ipcaPoints);
            // Real CDI over multi-week windows should be well above ~0.2%.
            if (window.CdiBusinessDays < 2 && (end - start).TotalDays > 5)
                window = await BuildFallbackAsync(start, end);
            else if (window.CdiAccumulatedPercent < 0.05m && (end - start).TotalDays > 30)
                window = await BuildFallbackAsync(start, end);

            lock (_lock) { _cache[key] = window; }
            return window;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load historical indices for {Start}..{End}", start, end);
            var fallback = await BuildFallbackAsync(start, end);
            lock (_lock) { _cache[key] = fallback; }
            return fallback;
        }
    }

    private async Task<HistoricalIndexWindow> BuildFallbackAsync(DateTime start, DateTime end)
    {
        var market = await _marketDataService.GetMarketDataAsync();
        var selic = market.SelicPercentPerYear ?? 13.25m;
        var ipca = market.IpcaPercentPerYear ?? 4.5m;
        var days = Math.Max(0, (end - start).Days);
        var years = days / 365.25m;
        var cdiFactor = (decimal)Math.Pow((double)(1 + selic / 100m), (double)years);
        var ipcaFactor = (decimal)Math.Pow((double)(1 + ipca / 100m), (double)years);
        return new HistoricalIndexWindow
        {
            CdiAccumulatedFactor = cdiFactor,
            CdiAccumulatedPercent = (cdiFactor - 1m) * 100m,
            IpcaAccumulatedFactor = ipcaFactor,
            IpcaAccumulatedPercent = (ipcaFactor - 1m) * 100m,
            CdiBusinessDays = (int)Math.Round(days * 252m / 365.25m),
            UsedFallbackRates = true,
            Notes = $"Fallback using current Selic {selic:F2}% and IPCA {ipca:F2}% a.a."
        };
    }

    private static HistoricalIndexWindow BuildWindow(
        DateTime start,
        DateTime end,
        List<(DateTime Date, decimal Value)> cdiDailyPoints,
        List<(DateTime Date, decimal Value)> ipcaMonthlyPoints)
    {
        // SGS 11 values are % a.d. Compound only on published business days in [start, end).
        decimal cdiFactor = 1m;
        var businessDays = 0;
        foreach (var p in cdiDailyPoints.Where(p => p.Date >= start && p.Date < end).OrderBy(p => p.Date))
        {
            // 0.055131 means +0.055131% that day → multiply by 1.00055131
            cdiFactor *= 1m + p.Value / 100m;
            businessDays++;
        }

        decimal ipcaFactor = 1m;
        foreach (var p in ipcaMonthlyPoints.Where(p => p.Date > start && p.Date <= end))
            ipcaFactor *= 1 + p.Value / 100m;

        return new HistoricalIndexWindow
        {
            CdiAccumulatedFactor = cdiFactor,
            CdiAccumulatedPercent = (cdiFactor - 1m) * 100m,
            IpcaAccumulatedFactor = ipcaFactor,
            IpcaAccumulatedPercent = (ipcaFactor - 1m) * 100m,
            CdiBusinessDays = businessDays,
            UsedFallbackRates = false
        };
    }

    private async Task<List<(DateTime Date, decimal Value)>> FetchSeriesAsync(
        HttpClient client,
        int seriesId,
        DateTime start,
        DateTime end,
        CancellationToken ct)
    {
        var di = start.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var df = end.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var url =
            $"https://api.bcb.gov.br/dados/serie/bcdata.sgs.{seriesId}/dados?formato=json&dataInicial={di}&dataFinal={df}";

        var points = await client.GetFromJsonAsync<List<BcbSgsPoint>>(url, ct) ?? [];
        var result = new List<(DateTime, decimal)>();
        foreach (var p in points)
        {
            if (!DateTime.TryParseExact(p.Data, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                continue;
            if (!decimal.TryParse(p.Valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                continue;
            result.Add((date, value));
        }

        return result;
    }

    private sealed class BcbSgsPoint
    {
        [JsonPropertyName("data")]
        public string Data { get; set; } = "";

        [JsonPropertyName("valor")]
        public string Valor { get; set; } = "";
    }
}
