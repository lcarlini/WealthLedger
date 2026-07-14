export interface MarketDataResponse {
  usdBrl: number;
  eurBrl: number;
  selicPercentPerYear?: number;
  ipcaPercentPerYear?: number;
  poupancaPercentPerYear?: number;
  btcUsd?: number;
  fedFundsRate?: number;
  lastUpdatedFx?: string;
  lastUpdatedIndices?: string;
  fxFromCache: boolean;
  indicesFromCache: boolean;
}
