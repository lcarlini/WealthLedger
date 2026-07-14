export type StatementSource = 'Card' | 'Checking';

export interface StatementImportResponse {
  id: string;
  fileName: string;
  source: number;
  transactionCount: number;
  createdDate: string;
}

export interface BankTransactionResponse {
  id: string;
  statementImportId: string;
  transactionType: string;
  date: string;
  amount: number;
  description: string;
  category?: string;
  installmentNumber?: number;
  installmentTotal?: number;
}
