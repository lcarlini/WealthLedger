export interface RefreshPriceItemResult {
  investmentId: string;
  name: string;
  ticker?: string;
  status: string;
  message?: string;
  previousAmount?: number;
  newAmount?: number;
  price?: number;
  quoteCurrency?: string;
}

export interface RefreshPricesResult {
  updated: number;
  skipped: number;
  failed: number;
  items: RefreshPriceItemResult[];
}
