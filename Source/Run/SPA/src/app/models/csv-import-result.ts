export interface CsvImportRowError {
  lineNumber: number;
  field?: string;
  message: string;
}

export interface CsvImportResult {
  rowsTotal: number;
  created: number;
  updated: number;
  skipped: number;
  failed: number;
  rowErrors: CsvImportRowError[];
}
