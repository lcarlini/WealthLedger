export enum AccountType {
  CheckingAccount = 1,
  SavingsBox = 2,
  FixedTerm = 3,
}

export const AccountTypeLabels: Record<AccountType, string> = {
  [AccountType.CheckingAccount]: 'Checking Account',
  [AccountType.SavingsBox]: 'Savings Box',
  [AccountType.FixedTerm]: 'Fixed Term',
};

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
}
