import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ApiResponse } from '../../models/api-response';

export interface CategorySummary {
  category: string;
  totalAmount: number;
  transactionCount: number;
}

export interface CategoryAmount {
  category: string;
  amount: number;
}

export interface MonthCategoryBreakdown {
  year: number;
  month: number;
  categories: CategoryAmount[];
}

export interface SpendingSummaryResponse {
  source: string;
  byCategory: CategorySummary[];
  monthlyBreakdown: MonthCategoryBreakdown[];
}

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/analytics';

  async getSpendingSummary(source: 'Card' | 'Checking'): Promise<SpendingSummaryResponse> {
    const params = new HttpParams().set('source', source);
    const res = await firstValueFrom(
      this.http.get<ApiResponse<SpendingSummaryResponse>>(`${this.base}/spending-summary`, { params })
    );
    return res.data;
  }

  async recategorize(): Promise<{ updated: number }> {
    const res = await firstValueFrom(
      this.http.post<ApiResponse<{ updated: number }>>(`${this.base}/recategorize`, {})
    );
    return res.data;
  }
}
