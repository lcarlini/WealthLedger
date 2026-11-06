// Mirrors WealthLedger.Contracts.Api.Trades. Enums are serialised as numbers by the API.
export enum TradeSide {
  Buy = 1,
  Sell = 2,
  Dividend = 3,
  Bonus = 4,
}

export type CostMethodName = 'weighted' | 'fifo';

export interface TradeResponse {
  id: string;
  investmentId: string;
  side: TradeSide;
  tradeDate: string;
  quantity: number;
  unitPrice: number;
  fees: number;
  notes: string | null;
}

export interface TradeRequest {
  investmentId: string;
  side: TradeSide;
  tradeDate: string;
  quantity: number;
  unitPrice: number;
  fees: number;
  notes: string | null;
}

export interface PositionSummary {
  investmentId: string;
  quantity: number;
  averagePrice: number;
  costBasis: number;
  realizedPnl: number;
  tradeCount: number;
  dividendIncome: number;
}

export interface TradePerformance {
  investmentId: string;
  method: string;
  marketValue: number;
  costBasis: number;
  realizedPnl: number;
  dividendIncome: number;
  unrealizedPnl: number;
  xirrAnnualPercent: number | null;
}

export interface TaxMonth {
  category: string;
  month: string;
  salesVolume: number;
  netGain: number;
  lossCarriedIn: number;
  lossCarriedOut: number;
  exemptByThreshold: boolean;
  taxableBase: number;
  ratePercent: number | null;
  taxDue: number;
  darfDueDate: string | null;
}

export interface TaxReport {
  year: number;
  disclaimer: string;
  months: TaxMonth[];
  totalTaxDue: number;
}
