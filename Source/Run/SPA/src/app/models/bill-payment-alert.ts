export enum BillRecurrence {
  Monthly = 1,
  Yearly = 2,
  Once = 3,
}

export enum BillCurrency {
  BRL = 1,
  USD = 2,
  EUR = 3,
}

export const BillRecurrenceLabels: Record<BillRecurrence, string> = {
  [BillRecurrence.Monthly]: 'Monthly',
  [BillRecurrence.Yearly]: 'Yearly',
  [BillRecurrence.Once]: 'One-time',
};

export interface BillPaymentAlert {
  id: string;
  name: string;
  notes?: string;
  amount: number;
  currency: string;
  dueDay: number;
  recurrence: string;
  startYear: number;
  startMonth: number;
  endYear?: number;
  endMonth?: number;
  isActive: boolean;
  createdDate: string;
  updatedDate: string;
}

export interface BillPaymentAlertRequest {
  id?: string;
  name: string;
  notes?: string;
  amount: number;
  currency: BillCurrency;
  dueDay: number;
  recurrence: BillRecurrence;
  startYear: number;
  startMonth: number;
  endYear?: number | null;
  endMonth?: number | null;
  isActive: boolean;
}

export interface BillPaymentOccurrence {
  id: string;
  billPaymentAlertId: string;
  name: string;
  notes?: string;
  amount: number;
  currency: string;
  recurrence: string;
  dueYear: number;
  dueMonth: number;
  dueDay: number;
  isPaid: boolean;
  paidDate?: string;
  isOverdue: boolean;
  isVirtual: boolean;
}
