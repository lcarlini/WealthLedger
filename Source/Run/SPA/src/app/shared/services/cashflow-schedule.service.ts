import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ApiResponse } from '../../models/api-response';
import {
  CashFlowScheduleItem,
  CashFlowScheduleItemRequest,
  SimulationMatrixResponse,
  ProposedCardInstallmentResponse,
} from '../../models/cashflow-schedule';

@Injectable({ providedIn: 'root' })
export class CashFlowScheduleService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/cashflow-schedule';

  async getAll(): Promise<CashFlowScheduleItem[]> {
    const res = await firstValueFrom(
      this.http.get<ApiResponse<CashFlowScheduleItem[]>>(this.base)
    );
    return res.data ?? [];
  }

  async getById(id: string): Promise<CashFlowScheduleItem> {
    const res = await firstValueFrom(
      this.http.get<ApiResponse<CashFlowScheduleItem>>(`${this.base}/${id}`)
    );
    return res.data;
  }

  async create(body: CashFlowScheduleItemRequest): Promise<CashFlowScheduleItem> {
    const res = await firstValueFrom(
      this.http.post<ApiResponse<CashFlowScheduleItem>>(this.base, body)
    );
    return res.data;
  }

  async update(id: string, body: CashFlowScheduleItemRequest): Promise<CashFlowScheduleItem> {
    const res = await firstValueFrom(
      this.http.put<ApiResponse<CashFlowScheduleItem>>(`${this.base}/${id}`, body)
    );
    return res.data;
  }

  async delete(id: string): Promise<void> {
    await firstValueFrom(this.http.delete(`${this.base}/${id}`));
  }

  async getSimulation(fromYear: number, fromMonth: number, monthCount = 36, startingBalance = 0): Promise<SimulationMatrixResponse> {
    const params = new HttpParams()
      .set('fromYear', fromYear)
      .set('fromMonth', fromMonth)
      .set('monthCount', monthCount)
      .set('startingBalance', startingBalance);
    const res = await firstValueFrom(
      this.http.get<ApiResponse<SimulationMatrixResponse>>(`${this.base}/simulation`, { params })
    );
    return res.data;
  }

  async getProposedCardInstallments(): Promise<ProposedCardInstallmentResponse[]> {
    const res = await firstValueFrom(
      this.http.get<ApiResponse<ProposedCardInstallmentResponse[]>>(`${this.base}/proposed-card-installments`)
    );
    return res.data ?? [];
  }

  async addFromCard(bankTransactionId: string): Promise<CashFlowScheduleItem> {
    const res = await firstValueFrom(
      this.http.post<ApiResponse<CashFlowScheduleItem>>(`${this.base}/from-card/${bankTransactionId}`, {})
    );
    return res.data;
  }

  async addAllFromCard(): Promise<{ added: number }> {
    const res = await firstValueFrom(
      this.http.post<ApiResponse<{ added: number }>>(`${this.base}/from-card/bulk`, {})
    );
    return res.data;
  }

  async addAllFromCardConsolidated(): Promise<{ added: number }> {
    const res = await firstValueFrom(
      this.http.post<ApiResponse<{ added: number }>>(`${this.base}/from-card/bulk-consolidated`, {})
    );
    return res.data;
  }

  getExportCsvUrl(fromYear: number, fromMonth: number, monthCount = 36, startingBalance = 0): string {
    const params = new HttpParams()
      .set('fromYear', fromYear)
      .set('fromMonth', fromMonth)
      .set('monthCount', monthCount)
      .set('startingBalance', startingBalance);
    return `${this.base}/export-csv?${params.toString()}`;
  }
}
