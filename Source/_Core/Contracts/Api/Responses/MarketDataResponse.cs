namespace WealthLedger.Contracts.Api.Responses;

public class MarketDataResponse
{
    public decimal UsdBrl { get; set; }
    public decimal EurBrl { get; set; }
    public decimal? SelicPercentPerYear { get; set; }
    public decimal? IpcaPercentPerYear { get; set; }
    public decimal? PoupancaPercentPerYear { get; set; }
    public decimal? BtcUsd { get; set; }
    public decimal? FedFundsRate { get; set; }
    public DateTime? LastUpdatedFx { get; set; }
    public DateTime? LastUpdatedIndices { get; set; }
    public bool FxFromCache { get; set; }
    public bool IndicesFromCache { get; set; }
}
