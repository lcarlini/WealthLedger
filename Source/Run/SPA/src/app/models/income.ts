export interface IncomeProfile {
  id: string;
  hourlyRateUsd: number;
  hoursPerDay: number;
  usdBrlRate: number;
  taxPercent: number;
}

export interface IncomeProfileRequest {
  hourlyRateUsd: number;
  hoursPerDay: number;
  usdBrlRate: number;
  taxPercent: number;
}

export interface ExtraIncome {
  id: string;
  year: number;
  month: number;
  amount: number;
  description: string;
}

export interface ExtraIncomeRequest {
  year: number;
  month: number;
  amount: number;
  description: string;
}

export interface IncomeMonthPreview {
  year: number;
  month: number;
  businessDays: number;
  defaultBusinessDays: number;
  isBusinessDaysOverridden: boolean;
  grossBrl: number;
  taxBrl: number;
  netBrl: number;
  extraBrl: number;
  totalBrl: number;
}

export interface BusinessDayOverrideRequest {
  year: number;
  month: number;
  days: number;
}

export interface IncomePreviewResponse {
  months: IncomeMonthPreview[];
}
