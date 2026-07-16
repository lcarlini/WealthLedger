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
}
