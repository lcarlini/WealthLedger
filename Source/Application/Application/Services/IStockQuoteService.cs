using WealthLedger.Contracts.Api.Responses;
using WealthLedger.Contracts.Domain.Enums;

namespace WealthLedger.Application.Services;

public interface IStockQuoteService
{
    /// <summary>
    /// Fetches the latest market price for a ticker.
    /// Crypto uses CoinGecko; B3 equities/FIIs/ETFs/BDRs/funds use brapi.dev.
    /// </summary>
    Task<StockQuote?> GetQuoteAsync(string ticker, AccountType accountType, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, StockQuote>> GetQuotesAsync(
        IEnumerable<(string Ticker, AccountType AccountType)> requests,
        CancellationToken cancellationToken = default);
}
