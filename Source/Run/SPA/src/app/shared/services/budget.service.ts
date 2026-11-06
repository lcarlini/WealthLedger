import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiResponse } from '../../models/api-response';

export type BudgetLevel = 'ok' | 'warning' | 'over';

export interface BudgetItemStatus {
  id: string;
  category: string;
  monthlyLimit: number;
  spent: number;
  remaining: number;
  percentUsed: number;
  level: BudgetLevel;
}

export interface CategorySpending {
  category: string;
  amount: number;
}

export interface BudgetStatus {
  year: number;
  month: number;
  items: BudgetItemStatus[];
  /** Spending in categories without a budget, largest first. */
  unbudgeted: CategorySpending[];
  totalLimit: number;
  totalSpent: number;
}

export const BUDGET_LIMITS = { categoryMaxLength: 60, limitMax: 1_000_000_000 } as const;

@Injectable({ providedIn: 'root' })
export class BudgetService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/budgets';

  status(year: number, month: number): Promise<BudgetStatus> {
    return this.call(this.http.get<ApiResponse<BudgetStatus>>(`${this.baseUrl}/status`, { params: { year, month } }));
  }

  categories(): Promise<string[]> {
    return this.call(this.http.get<ApiResponse<string[]>>(`${this.baseUrl}/categories`)).then(c => c ?? []);
  }

  /** Creates the budget, or updates the limit when the category already has one. */
  save(category: string, monthlyLimit: number): Promise<unknown> {
    return this.call(this.http.put<ApiResponse<unknown>>(this.baseUrl, { category, monthlyLimit }));
  }

  remove(id: string): Promise<unknown> {
    return this.call(this.http.delete<ApiResponse<unknown>>(`${this.baseUrl}/${encodeURIComponent(id)}`));
  }

  /** Unwraps the API envelope and surfaces the server's error message instead of a generic HTTP error. */
  private async call<T>(request: Observable<ApiResponse<T>>): Promise<T> {
    try {
      const response = await firstValueFrom(request);
      return response.data;
    } catch (error) {
      if (error instanceof HttpErrorResponse) {
        const message = (error.error as ApiResponse<unknown> | null)?.errors?.[0]?.message;
        throw new Error(message ?? error.message);
      }
      throw error;
    }
  }
}
