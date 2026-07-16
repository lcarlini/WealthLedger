/** Product types available in the side-by-side comparison calculator. */
export enum ComparisonProductType {
  Savings = 'savings',
  Cdb = 'cdb',
  Lci = 'lci',
  Lca = 'lca',
  TreasuryBond = 'treasury',
  FixedTerm = 'fixedTerm',
  CheckingAccount = 'checking',
  Stock = 'stock',
  Fii = 'fii',
  Etf = 'etf',
  InvestmentFund = 'investmentFund',
  Bdr = 'bdr',
  Crypto = 'crypto',
}

export const ComparisonProductLabels: Record<ComparisonProductType, string> = {
  [ComparisonProductType.Savings]: 'Savings (Poupança)',
  [ComparisonProductType.Cdb]: 'CDB',
  [ComparisonProductType.Lci]: 'LCI',
  [ComparisonProductType.Lca]: 'LCA',
  [ComparisonProductType.TreasuryBond]: 'Treasury Bond (Tesouro)',
  [ComparisonProductType.FixedTerm]: 'Fixed Term (CDI-linked)',
  [ComparisonProductType.CheckingAccount]: 'Checking Account',
  [ComparisonProductType.Stock]: 'Stock',
  [ComparisonProductType.Fii]: 'FII',
  [ComparisonProductType.Etf]: 'ETF',
  [ComparisonProductType.InvestmentFund]: 'Mutual Fund',
  [ComparisonProductType.Bdr]: 'BDR',
  [ComparisonProductType.Crypto]: 'Cryptocurrency',
};

export type RateMode = 'fixed' | 'cdi' | 'poupanca';

export interface ComparisonScenarioInput {
  id: string;
  name: string;
  productType: ComparisonProductType;
  initialAmount: number;
  monthlyContribution: number;
  rateMode: RateMode;
  /** Fixed annual rate (% a.a.) when rateMode is fixed. */
  annualRatePercent: number;
  /** % of CDI when rateMode is cdi (e.g. 100 = 100% CDI). */
  cdiPercent: number;
  /** Annual admin/management fee (% a.a. deducted from balance). */
  adminFeePercentPerYear: number;
  /** Annual custody/broker fee (% a.a.). */
  custodyFeePercentPerYear: number;
}

export interface ComparisonGlobalSettings {
  /** Simulation length in years (supports fractional, e.g. 0.5 = 6 months). */
  years: number;
  /** CDI proxy — typically Selic % a.a. from market data. */
  cdiBasePercent: number;
  /** Poupança % a.a. from market data or manual. */
  poupancaPercent: number;
  /** IPCA % a.a. for real-return adjustment. */
  inflationPercent: number;
  deductTaxes: boolean;
  deductFees: boolean;
  adjustForInflation: boolean;
}

export interface MonthlyProjectionPoint {
  month: number;
  year: number;
  balance: number;
  totalContributed: number;
  grossGain: number;
}

export interface ComparisonScenarioResult {
  scenarioId: string;
  name: string;
  productType: ComparisonProductType;
  productLabel: string;
  effectiveAnnualRatePercent: number;
  totalContributed: number;
  grossFinal: number;
  grossGain: number;
  fees: number;
  incomeTax: number;
  netFinal: number;
  netGain: number;
  realNetFinal: number;
  realNetGain: number;
  realReturnPercent: number;
  netReturnPercent: number;
  monthlyPoints: MonthlyProjectionPoint[];
  tips: string[];
}

export interface ComparisonRunResult {
  scenarios: ComparisonScenarioResult[];
  rankedByNetGain: ComparisonScenarioResult[];
  rankedByRealGain: ComparisonScenarioResult[];
  globalTips: string[];
  settings: ComparisonGlobalSettings;
  months: number;
}

export function createDefaultScenario(productType: ComparisonProductType, index: number): ComparisonScenarioInput {
  const base: ComparisonScenarioInput = {
    id: crypto.randomUUID(),
    name: `${ComparisonProductLabels[productType]} ${index}`,
    productType,
    initialAmount: 10_000,
    monthlyContribution: 0,
    rateMode: 'cdi',
    annualRatePercent: 12,
    cdiPercent: 100,
    adminFeePercentPerYear: 0,
    custodyFeePercentPerYear: 0,
  };

  switch (productType) {
    case ComparisonProductType.Savings:
      return { ...base, name: 'Poupança', rateMode: 'poupanca', cdiPercent: 0 };
    case ComparisonProductType.Cdb:
      return { ...base, name: 'CDB 100% CDI', cdiPercent: 100 };
    case ComparisonProductType.Lci:
      return { ...base, name: 'LCI 95% CDI', cdiPercent: 95 };
    case ComparisonProductType.Lca:
      return { ...base, name: 'LCA 90% CDI', cdiPercent: 90 };
    case ComparisonProductType.TreasuryBond:
      return { ...base, name: 'Tesouro Selic', rateMode: 'cdi', cdiPercent: 100 };
    case ComparisonProductType.FixedTerm:
      return { ...base, name: 'CDB-style Fixed Term', cdiPercent: 110 };
    case ComparisonProductType.CheckingAccount:
      return { ...base, name: 'Checking (100% CDI)', cdiPercent: 100, monthlyContribution: 0 };
    case ComparisonProductType.Stock:
      return { ...base, name: 'Stock portfolio', rateMode: 'fixed', annualRatePercent: 10 };
    case ComparisonProductType.Fii:
      return { ...base, name: 'FII basket', rateMode: 'fixed', annualRatePercent: 9 };
    case ComparisonProductType.Etf:
      return { ...base, name: 'ETF (IVVB11-style)', rateMode: 'fixed', annualRatePercent: 11, adminFeePercentPerYear: 0.2 };
    case ComparisonProductType.InvestmentFund:
      return { ...base, name: 'Mutual fund', rateMode: 'fixed', annualRatePercent: 10, adminFeePercentPerYear: 1.5 };
    case ComparisonProductType.Bdr:
      return { ...base, name: 'BDR portfolio', rateMode: 'fixed', annualRatePercent: 10 };
    case ComparisonProductType.Crypto:
      return { ...base, name: 'Crypto (BTC/ETH)', rateMode: 'fixed', annualRatePercent: 8 };
    default:
      return base;
  }
}

export function createStarterScenarios(): ComparisonScenarioInput[] {
  return [
    createDefaultScenario(ComparisonProductType.Cdb, 1),
    createDefaultScenario(ComparisonProductType.Lci, 2),
    createDefaultScenario(ComparisonProductType.TreasuryBond, 3),
    createDefaultScenario(ComparisonProductType.Stock, 4),
  ];
}
