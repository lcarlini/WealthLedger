import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ApiResponse } from '../../models/api-response';
import {
  BillPaymentAlert,
  BillPaymentAlertRequest,
  BillPaymentOccurrence,
} from '../../models/bill-payment-alert';

@Injectable({ providedIn: 'root' })
export class BillPaymentAlertService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/bill-alerts';

  async getAll(): Promise<BillPaymentAlert[]> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<BillPaymentAlert[]>>(this.baseUrl)
    );
    return response.data ?? [];
  }

  async get(id: string): Promise<BillPaymentAlert> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<BillPaymentAlert>>(`${this.baseUrl}/${id}`)
    );
    return response.data;
  }

  async create(body: BillPaymentAlertRequest): Promise<BillPaymentAlert> {
    const response = await firstValueFrom(
      this.http.post<ApiResponse<BillPaymentAlert>>(this.baseUrl, body)
    );
    return response.data;
  }

  async update(id: string, body: BillPaymentAlertRequest): Promise<BillPaymentAlert> {
    const response = await firstValueFrom(
      this.http.put<ApiResponse<BillPaymentAlert>>(`${this.baseUrl}/${id}`, body)
    );
    return response.data;
  }

  async delete(id: string): Promise<void> {
    await firstValueFrom(this.http.delete(`${this.baseUrl}/${id}`));
  }

  async getDueThisMonth(): Promise<BillPaymentOccurrence[]> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<BillPaymentOccurrence[]>>(`${this.baseUrl}/due-this-month`)
    );
    return response.data ?? [];
  }

  async getUpcoming(monthsAhead = 12): Promise<BillPaymentOccurrence[]> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<BillPaymentOccurrence[]>>(
        `${this.baseUrl}/upcoming?monthsAhead=${monthsAhead}`
      )
    );
    return response.data ?? [];
  }

  async getPast(): Promise<BillPaymentOccurrence[]> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<BillPaymentOccurrence[]>>(`${this.baseUrl}/past`)
    );
    return response.data ?? [];
  }

  async markPaid(occurrenceId: string): Promise<BillPaymentOccurrence> {
    const response = await firstValueFrom(
      this.http.put<ApiResponse<BillPaymentOccurrence>>(
        `${this.baseUrl}/occurrences/${occurrenceId}/paid`,
        {}
      )
    );
    return response.data;
  }

  async getPendingCount(): Promise<number> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<number>>(`${this.baseUrl}/pending-count`)
    );
    return response.data ?? 0;
  }
}
