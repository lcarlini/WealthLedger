export interface DashboardResponse {
  institutionCount: number;
  investmentCount: number;
  archivedInvestmentCount: number;
  totalAmount: number;
  totalPrincipal: number;
  unrealizedGain: number;
  plannedDebitsProjection: number;
  projectionYears: number;
  debitsNextMonth: number;
  futureCardDebits: number;
  investmentsByType: InvestmentsByTypeItem[];
  pendingTaskCount: number;
  estimatedAnnualReturnPercent?: number;
  selicPercent?: number;
  ipcaPercent?: number;
  excessOverCdiPercent?: number;
  excessOverIpcaPercent?: number;
  projectedValue1Y: number;
  projectedValue3Y: number;
  projectedValue5Y: number;
  portfolioVerdict: string;
  topHoldings: DashboardHoldingHighlight[];
}

export interface DashboardHoldingHighlight {
  investmentId: string;
  name: string;
  amountBrl: number;
  gainPercent: number;
  rateLabel: string;
}

export interface InvestmentsByTypeItem {
  accountType: string;
  count: number;
  totalAmount: number;
}
