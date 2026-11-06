import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiResponse } from '../../models/api-response';
import {
  CostMethodName,
  PositionSummary,
  TaxReport,
  TradePerformance,
  TradeRequest,
  TradeResponse,
} from '../../models/trade';

@Injectable({ providedIn: 'root' })
export class TradeService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/trades';

  list(investmentId: string): Promise<TradeResponse[]> {
    return this.call(this.http.get<ApiResponse<TradeResponse[]>>(this.baseUrl, { params: { investmentId } }))
      .then(items => items ?? []);
  }

  position(investmentId: string, method: CostMethodName = 'weighted'): Promise<PositionSummary> {
    return this.call(this.http.get<ApiResponse<PositionSummary>>(`${this.baseUrl}/position/${investmentId}`, { params: { method } }));
  }

  performance(investmentId: string, method: CostMethodName = 'weighted'): Promise<TradePerformance> {
    return this.call(this.http.get<ApiResponse<TradePerformance>>(`${this.baseUrl}/performance/${investmentId}`, { params: { method } }));
  }

  taxReport(year: number): Promise<TaxReport> {
    return this.call(this.http.get<ApiResponse<TaxReport>>(`${this.baseUrl}/tax-report`, { params: { year } }));
  }

  add(request: TradeRequest): Promise<TradeResponse> {
    return this.call(this.http.post<ApiResponse<TradeResponse>>(this.baseUrl, request));
  }

  remove(tradeId: string): Promise<boolean> {
    return this.call(this.http.delete<ApiResponse<boolean>>(`${this.baseUrl}/${tradeId}`));
  }

  /** Unwraps the API envelope and surfaces the server's message instead of a generic HTTP error. */
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
