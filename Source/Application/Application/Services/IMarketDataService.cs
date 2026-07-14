using WealthLedger.Contracts.Api.Responses;

namespace WealthLedger.Application.Services;

public interface IMarketDataService
{
    Task<MarketDataResponse> GetMarketDataAsync();
}
