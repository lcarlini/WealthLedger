using WealthLedger.Application.Repositories;
using WealthLedger.Application.Services;
using WealthLedger.Contracts.Api.Queries;
using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain;
using WealthLedger.Contracts.Domain.Enums;
using WealthLedger.Contracts.Domain.Pagination;
using Microsoft.Extensions.Logging;

namespace WealthLedger.Infrastructure.Services;

public class InvestmentService : IInvestmentService
{
    private readonly IRepository<Investment> _repository;
    private readonly IStockQuoteService _stockQuoteService;
    private readonly IMarketDataService _marketDataService;
    private readonly ILogger<InvestmentService> _logger;

    public InvestmentService(
        IRepository<Investment> repository,
        IStockQuoteService stockQuoteService,
        IMarketDataService marketDataService,
        ILogger<InvestmentService> logger)
    {
        _repository = repository;
        _stockQuoteService = stockQuoteService;
        _marketDataService = marketDataService;
        _logger = logger;
    }

    public Task<IEnumerable<Investment>> GetAllAsync() => _repository.GetAllAsync();
    public Task<PagedResponse<Investment>> GetPagedAsync(QueryOptions query) => _repository.GetPagedAsync(query);
    public Task<Investment?> GetAsync(Guid id) => _repository.GetAsync(id);
    public Task<Investment> UpsertAsync(Investment entity) => _repository.UpsertAsync(entity);
    public Task DeleteAsync(Guid id) => _repository.DeleteAsync(id);

    public Task<bool> CheckDuplicateAsync(string fieldName, string fieldValue, Guid? excludeEntityId = null) =>
        _repository.ExistsAsync(fieldName, fieldValue, excludeEntityId);

    public async Task<IEnumerable<Investment>> GetByInstitutionIdAsync(Guid institutionId)
    {
        var all = await _repository.GetAllAsync();
        return all.Where(i => i.FinancialInstitutionId == institutionId);
    }

    public async Task<RefreshPricesResult> RefreshPricesAsync(CancellationToken cancellationToken = default)
    {
        var result = new RefreshPricesResult();
        var investments = (await _repository.GetAllAsync()).ToList();
        var candidates = investments
            .Where(i => AccountTypeRules.IsVariableIncome(i.AccountType)
                        && !string.IsNullOrWhiteSpace(i.Ticker)
                        && i.Quantity is > 0)
            .ToList();

        if (candidates.Count == 0)
            return result;

        MarketDataResponse? market = null;
        try
        {
            market = await _marketDataService.GetMarketDataAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load FX rates for price refresh; conversion may be limited");
        }

        foreach (var inv in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = new RefreshPriceItemResult
            {
                InvestmentId = inv.Id,
                Name = inv.Name,
                Ticker = inv.Ticker,
                PreviousAmount = inv.Amount
            };

            try
            {
                var quote = await _stockQuoteService.GetQuoteAsync(inv.Ticker!, inv.AccountType, cancellationToken);
                if (quote == null || quote.Price <= 0)
                {
                    item.Status = "Failed";
                    item.Message = "Quote not found.";
                    result.Failed++;
                    result.Items.Add(item);
                    continue;
                }

                if (!TryConvertPrice(quote, inv.Currency, market, out var priceInInvCurrency, out var convertError))
                {
                    item.Status = "Failed";
                    item.Message = convertError;
                    item.Price = quote.Price;
                    item.QuoteCurrency = quote.Currency;
                    result.Failed++;
                    result.Items.Add(item);
                    continue;
                }

                var newAmount = Math.Round(inv.Quantity!.Value * priceInInvCurrency, 2, MidpointRounding.AwayFromZero);
                inv.Amount = newAmount;
                inv.UpdatedDate = DateTime.UtcNow;
                await _repository.UpsertAsync(inv);

                item.Status = "Updated";
                item.NewAmount = newAmount;
                item.Price = priceInInvCurrency;
                item.QuoteCurrency = inv.Currency.ToString();
                item.Message = $"{quote.Source}: {quote.Price} {quote.Currency}" +
                               (quote.Currency != inv.Currency.ToString()
                                   ? $" → {priceInInvCurrency} {inv.Currency}"
                                   : "");
                result.Updated++;
                result.Items.Add(item);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh price for {Name} ({Ticker})", inv.Name, inv.Ticker);
                item.Status = "Failed";
                item.Message = ex.Message;
                result.Failed++;
                result.Items.Add(item);
            }
        }

        // Holdings without ticker/quantity are skipped (counted for transparency).
        var skipped = investments.Count(i =>
            AccountTypeRules.IsVariableIncome(i.AccountType)
            && (string.IsNullOrWhiteSpace(i.Ticker) || i.Quantity is null or <= 0));
        result.Skipped = skipped;

        return result;
    }

    private static bool TryConvertPrice(
        StockQuote quote,
        Currency targetCurrency,
        MarketDataResponse? market,
        out decimal priceInTarget,
        out string? error)
    {
        priceInTarget = 0;
        error = null;

        var quoteCur = quote.Currency.ToUpperInvariant();
        var target = targetCurrency.ToString().ToUpperInvariant();

        if (quoteCur == target)
        {
            priceInTarget = quote.Price;
            return true;
        }

        // Convert via BRL as pivot when possible.
        if (!TryToBrl(quote.Price, quoteCur, market, out var inBrl, out error))
            return false;

        if (target == "BRL")
        {
            priceInTarget = inBrl;
            return true;
        }

        if (!TryFromBrl(inBrl, target, market, out priceInTarget, out error))
            return false;

        return true;
    }

    private static bool TryToBrl(
        decimal amount,
        string fromCurrency,
        MarketDataResponse? market,
        out decimal brl,
        out string? error)
    {
        brl = 0;
        error = null;
        if (fromCurrency == "BRL")
        {
            brl = amount;
            return true;
        }

        if (fromCurrency == "USD")
        {
            if (market?.UsdBrl is <= 0)
            {
                error = "USD/BRL rate unavailable for conversion.";
                return false;
            }

            brl = amount * market!.UsdBrl;
            return true;
        }

        if (fromCurrency == "EUR")
        {
            if (market?.EurBrl is <= 0)
            {
                error = "EUR/BRL rate unavailable for conversion.";
                return false;
            }

            brl = amount * market!.EurBrl;
            return true;
        }

        error = $"Unsupported quote currency {fromCurrency}.";
        return false;
    }

    private static bool TryFromBrl(
        decimal brl,
        string toCurrency,
        MarketDataResponse? market,
        out decimal amount,
        out string? error)
    {
        amount = 0;
        error = null;
        if (toCurrency == "BRL")
        {
            amount = brl;
            return true;
        }

        if (toCurrency == "USD")
        {
            if (market?.UsdBrl is <= 0)
            {
                error = "USD/BRL rate unavailable for conversion.";
                return false;
            }

            amount = brl / market!.UsdBrl;
            return true;
        }

        if (toCurrency == "EUR")
        {
            if (market?.EurBrl is <= 0)
            {
                error = "EUR/BRL rate unavailable for conversion.";
                return false;
            }

            amount = brl / market!.EurBrl;
            return true;
        }

        error = $"Unsupported investment currency {toCurrency}.";
        return false;
    }
}
