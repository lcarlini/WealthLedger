export interface DashboardResponse {
  institutionCount: number;
  investmentCount: number;
  totalAmount: number;
  plannedDebitsProjection: number;
  projectionYears: number;
  debitsNextMonth: number;
  futureCardDebits: number;
  investmentsByType: InvestmentsByTypeItem[];
  pendingTaskCount: number;
}

export interface InvestmentsByTypeItem {
  accountType: string;
  count: number;
  totalAmount: number;
}
