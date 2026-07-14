export interface TaskItem {
  id: string;
  investmentId: string;
  investmentName?: string;
  institutionName?: string;
  title: string;
  description?: string;
  dueYear: number;
  dueMonth: number;
  dueDay?: number;
  isCompleted: boolean;
  completedDate?: string;
  requiredAmount?: number;
  /** BRL, USD, EUR — from investment */
  currency?: string;
  createdDate: string;
}

export interface FutureTask {
  title: string;
  description?: string;
  investmentName?: string;
  institutionName?: string;
  dueYear: number;
  dueMonth: number;
  dueDay?: number;
  amount?: number;
  currency?: string;
  taskType: string; // 'Maturity' | 'MonthlyMovement'
}