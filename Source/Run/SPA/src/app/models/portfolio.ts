export interface AllocationSlice {
  key: string;
  label: string;
  amountBrl: number;
  percent: number;
  count: number;
}

export interface HoldingPerformanceItem {
  investmentId: string;
  name: string;
  institutionName?: string;
  accountType: string;
  currency: string;
  ticker?: string;
  quantity?: number;
  averagePrice?: number;
  marketValue: number;
  marketValueBrl: number;
  costBasis?: number;
  costBasisBrl?: number;
  unrealizedGain?: number;
  unrealizedGainBrl?: number;
  unrealizedGainPercent?: number;
  isVariableIncome: boolean;
}

export interface BenchmarkComparisonItem {
  code: string;
  name: string;
  annualRatePercent: number;
  portfolioEstimatedAnnualPercent: number;
  differencePercent: number;
  portfolioBeats: boolean;
  periodLabel: string;
}

export interface RebalanceSuggestion {
  category: string;
  currentPercent: number;
  targetPercent: number;
  driftPercent: number;
  action: string;
  message: string;
}

export interface CalendarEventItem {
  date: string;
  title: string;
  eventType: string;
  investmentName?: string;
  amount?: number;
  currency?: string;
}

export interface FinancialHealthScore {
  score: number;
  grade: string;
  liquidityScore: number;
  diversificationScore: number;
  inflationHedgeScore: number;
  emergencyCoverageScore: number;
  goalProgressScore: number;
  notes: string[];
}

export interface PortfolioScore {
  score: number;
  grade: string;
  diversificationScore: number;
  riskScore: number;
  longTermFitScore: number;
  concentrationTopHoldingPercent: number;
  distinctAssetClasses: number;
  notes: string[];
}

export interface PortfolioSnapshot {
  id: string;
  snapshotDate: string;
  totalAmountBrl: number;
  cashAmountBrl: number;
  fixedIncomeAmountBrl: number;
  variableIncomeAmountBrl: number;
  unrealizedGainBrl: number;
  investmentCount: number;
  notes?: string;
}

export interface PortfolioOverview {
  totalNetWorthBrl: number;
  totalCostBasisBrl: number;
  unrealizedGainBrl: number;
  unrealizedGainPercent: number;
  realizedPassiveIncomeBrl: number;
  realizedPassiveIncomeYtdBrl: number;
  cashAmountBrl: number;
  fixedIncomeAmountBrl: number;
  variableIncomeAmountBrl: number;
  investmentCount: number;
  institutionCount: number;
  health: FinancialHealthScore;
  portfolioScore: PortfolioScore;
  allocationByType: AllocationSlice[];
  allocationByCurrency: AllocationSlice[];
  allocationByInstitution: AllocationSlice[];
  holdings: HoldingPerformanceItem[];
  benchmarks: BenchmarkComparisonItem[];
  rebalanceSuggestions: RebalanceSuggestion[];
  calendar: CalendarEventItem[];
  timeline: PortfolioSnapshot[];
  insights: string[];
}

export enum PassiveIncomeType {
  Dividend = 1,
  Interest = 2,
  Jcp = 3,
  FiiYield = 4,
  Other = 5,
}

export const PassiveIncomeTypeLabels: Record<PassiveIncomeType, string> = {
  [PassiveIncomeType.Dividend]: 'Dividend',
  [PassiveIncomeType.Interest]: 'Interest',
  [PassiveIncomeType.Jcp]: 'JCP',
  [PassiveIncomeType.FiiYield]: 'FII Yield',
  [PassiveIncomeType.Other]: 'Other',
};

export interface PassiveIncome {
  id: string;
  investmentId?: string;
  investmentName?: string;
  name: string;
  incomeType: PassiveIncomeType;
  currency: number;
  amount: number;
  paymentDate: string;
  notes?: string;
}

export enum GoalType {
  NetWorth = 1,
  EmergencyFund = 2,
  Retirement = 3,
  Custom = 4,
}

export const GoalTypeLabels: Record<GoalType, string> = {
  [GoalType.NetWorth]: 'Net Worth',
  [GoalType.EmergencyFund]: 'Emergency Fund',
  [GoalType.Retirement]: 'Retirement',
  [GoalType.Custom]: 'Custom',
};

export interface InvestmentGoal {
  id: string;
  name: string;
  goalType: GoalType;
  currency: number;
  targetAmount: number;
  currentAmount: number;
  targetDate?: string;
  monthlyContribution?: number;
  expectedAnnualReturnPercent?: number;
  notes?: string;
  isCompleted: boolean;
  progressPercent: number;
  remainingAmount: number;
  monthsRemaining?: number;
  projectedAmountAtTarget?: number;
  onTrack?: boolean;
}

export interface WatchlistItem {
  id: string;
  ticker: string;
  name: string;
  accountType: number;
  targetPrice?: number;
  alertAbove?: number;
  alertBelow?: number;
  notes?: string;
  lastPrice?: number;
  lastPriceCurrency?: string;
  alertTriggered?: boolean;
  alertMessage?: string;
}
