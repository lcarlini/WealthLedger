export enum CashFlowItemType {
  Income = 1,
  Expense = 2,
  Debt = 3,
  CardInstallment = 4,
}

export enum CashFlowSource {
  Manual = 1,
  FromCardImport = 2,
}

export const CashFlowItemTypeLabels: Record<CashFlowItemType, string> = {
  [CashFlowItemType.Income]: 'Income',
  [CashFlowItemType.Expense]: 'Expense',
  [CashFlowItemType.Debt]: 'Debt',
  [CashFlowItemType.CardInstallment]: 'Card installment',
};

export interface CashFlowScheduleItem {
  id: string;
  name: string;
  itemType: CashFlowItemType;
  amountPerMonth: number;
  startYear: number;
  startMonth: number;
  numberOfMonths: number;
  source: CashFlowSource;
  bankTransactionId?: string;
  displayOrder: number;
  createdDate: string;
}

export interface CashFlowScheduleItemRequest {
  id?: string;
  name: string;
  itemType: CashFlowItemType;
  amountPerMonth: number;
  startYear: number;
  startMonth: number;
  numberOfMonths: number;
  source: CashFlowSource;
  bankTransactionId?: string;
  displayOrder: number;
}

export interface SimulationMonthColumn {
  year: number;
  month: number;
  label: string;
}

export interface SimulationRow {
  id: string;
  name: string;
  itemType: string;
  amountsByMonth: (number | null)[];
}

export interface SimulationMatrixResponse {
  startingBalance: number;
  monthColumns: SimulationMonthColumn[];
  rows: SimulationRow[];
  totalsPerMonth: number[];
  accumulatedPerMonth: number[];
}

export interface ProposedCardInstallmentResponse {
  bankTransactionId: string;
  description: string;
  amountPerMonth: number;
  startYear: number;
  startMonth: number;
  remainingInstallments: number;
  installmentNumber: number;
  installmentTotal: number;
}
