export enum AccountType {
  // Fixed-income / cash
  CheckingAccount = 1,
  SavingsBox = 2,
  FixedTerm = 3,
  // Variable-income (renda variável)
  Stock = 4,
  FII = 5,
  ETF = 6,
  InvestmentFund = 7,
  BDR = 8,
  Crypto = 9,
}

export const AccountTypeLabels: Record<AccountType, string> = {
  [AccountType.CheckingAccount]: 'Checking Account',
  [AccountType.SavingsBox]: 'Savings Box',
  [AccountType.FixedTerm]: 'Fixed Term',
  [AccountType.Stock]: 'Stock',
  [AccountType.FII]: 'FII',
  [AccountType.ETF]: 'ETF',
  [AccountType.InvestmentFund]: 'Investment Fund',
  [AccountType.BDR]: 'BDR',
  [AccountType.Crypto]: 'Crypto',
};

const VARIABLE_INCOME_TYPES = new Set<AccountType>([
  AccountType.Stock,
  AccountType.FII,
  AccountType.ETF,
  AccountType.InvestmentFund,
  AccountType.BDR,
  AccountType.Crypto,
]);

export function isVariableIncome(type: AccountType): boolean {
  return VARIABLE_INCOME_TYPES.has(type);
}

export function isFixedTerm(type: AccountType): boolean {
  return type === AccountType.FixedTerm;
}

export function hasDeterministicYield(type: AccountType): boolean {
  return !isVariableIncome(type);
}

export enum Currency {
  BRL = 1,
  USD = 2,
  EUR = 3,
}

export const CurrencyLabels: Record<Currency, string> = {
  [Currency.BRL]: 'BRL',
  [Currency.USD]: 'USD',
  [Currency.EUR]: 'EUR',
};

export enum InvestmentStatus {
  Active = 1,
  Archived = 2,
}

export const InvestmentStatusLabels: Record<InvestmentStatus, string> = {
  [InvestmentStatus.Active]: 'Ativo',
  [InvestmentStatus.Archived]: 'Arquivado',
};

export interface Investment {
  id: string;
  financialInstitutionId: string;
  institutionName?: string;
  name: string;
  accountType: AccountType;
  currency: Currency;
  amount: number;
  cdiPercentage: number;
  annualRatePercent?: number;
  maturityDate?: string;
  requiresMonthlyMovement: boolean;
  monthlyMovementAmount?: number;
  ticker?: string;
  quantity?: number;
  averagePrice?: number;
  startDate: string;
  principalAmount: number;
  status: InvestmentStatus;
  archivedAt?: string;
  redeemedAmount?: number;
  archiveNotes?: string;
  bookAmountAtArchive?: number;
  expectedTheoreticalAtArchive?: number;
  isIncomeTaxExempt: boolean;
  createdDate: string;
  updatedDate: string;
}

export interface InvestmentRequest {
  id?: string;
  financialInstitutionId: string;
  name: string;
  accountType: AccountType;
  currency: Currency;
  amount: number;
  cdiPercentage: number;
  annualRatePercent?: number;
  maturityDate?: string;
  requiresMonthlyMovement: boolean;
  monthlyMovementAmount?: number;
  ticker?: string;
  quantity?: number;
  averagePrice?: number;
  startDate?: string;
  principalAmount?: number;
  isIncomeTaxExempt?: boolean;
}

export interface ArchiveInvestmentRequest {
  redeemedAmount?: number;
  archivedAt?: string;
  notes?: string;
}

export interface PerformanceBreakdownItem {
  key: string;
  label: string;
  amount: number;
  note?: string;
}

export interface PerformanceProjectionPoint {
  monthsAhead: number;
  label: string;
  projectedGross: number;
  projectedNet: number;
  projectedNetGainPercent: number;
}

export interface InvestmentPerformance {
  investmentId: string;
  name: string;
  accountType: AccountType;
  currency: Currency;
  status: InvestmentStatus;
  startDate: string;
  asOfDate: string;
  holdingDays: number;
  principalAmount: number;
  currentAmount: number;
  accruedTheoreticalAmount?: number;
  grossGain: number;
  grossGainPercent: number;
  iofAmount: number;
  iofRatePercent: number;
  irAmount: number;
  irRatePercent: number;
  isIncomeTaxExempt: boolean;
  netAmount: number;
  netGain: number;
  netGainPercent: number;
  cdiAccumulatedPercent?: number;
  cdiEquivalentAccumulatedPercent?: number;
  ipcaAccumulatedPercent?: number;
  realGainPercent?: number;
  excessOverIpcaPercent?: number;
  excessOverCdiPercent?: number;
  verdict: string;
  verdictSummary: string;
  bookAmount?: number;
  theoreticalAmount?: number;
  vsBookDelta?: number;
  vsBookPercent?: number;
  vsTheoreticalDelta?: number;
  vsTheoreticalPercent?: number;
  redemptionVerdict?: string;
  redemptionSummary?: string;
  breakdown: PerformanceBreakdownItem[];
  projections: PerformanceProjectionPoint[];
}

/** Client-side redemption preview (active holdings) / shared verdict codes. */
export type RedemptionVerdictCode =
  | 'above_both'
  | 'above_book'
  | 'above_theoretical'
  | 'match'
  | 'below_book'
  | 'below_theoretical'
  | 'below_both'
  | 'mixed';

export interface RedemptionPreview {
  bookAmount: number;
  theoreticalAmount: number | null;
  vsBookDelta: number;
  vsBookPercent: number;
  vsTheoreticalDelta: number | null;
  vsTheoreticalPercent: number | null;
  verdict: RedemptionVerdictCode;
}
