import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ApiResponse } from '../../models/api-response';
import {
  IncomeProfile,
  IncomeProfileRequest,
  ExtraIncome,
  ExtraIncomeRequest,
  IncomePreviewResponse,
  BusinessDayOverrideRequest,
} from '../../models/income';

@Injectable({ providedIn: 'root' })
export class IncomeService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/income';

  async getProfile(): Promise<IncomeProfile | null> {
    const res = await firstValueFrom(
      this.http.get<ApiResponse<IncomeProfile | null>>(`${this.base}/profile`)
    );
    return res.data;
  }

  async saveProfile(body: IncomeProfileRequest): Promise<IncomeProfile> {
    const res = await firstValueFrom(
      this.http.put<ApiResponse<IncomeProfile>>(`${this.base}/profile`, body)
    );
    return res.data;
  }

  async getExtra(): Promise<ExtraIncome[]> {
    const res = await firstValueFrom(
      this.http.get<ApiResponse<ExtraIncome[]>>(`${this.base}/extra`)
    );
    return res.data ?? [];
  }

  async addExtra(body: ExtraIncomeRequest): Promise<ExtraIncome> {
    const res = await firstValueFrom(
      this.http.post<ApiResponse<ExtraIncome>>(`${this.base}/extra`, body)
    );
    return res.data;
  }

  async deleteExtra(id: string): Promise<void> {
    await firstValueFrom(this.http.delete(`${this.base}/extra/${id}`));
  }

  async getPreview(fromYear: number, fromMonth: number, monthCount = 36): Promise<IncomePreviewResponse> {
    const params = new HttpParams()
      .set('fromYear', fromYear)
      .set('fromMonth', fromMonth)
      .set('monthCount', monthCount);
    const res = await firstValueFrom(
      this.http.get<ApiResponse<IncomePreviewResponse>>(`${this.base}/preview`, { params })
    );
    return res.data;
  }

  async setBusinessDayOverride(body: BusinessDayOverrideRequest): Promise<void> {
    await firstValueFrom(
      this.http.put(`${this.base}/business-days`, body)
    );
  }

  async resetBusinessDayOverride(year: number, month: number): Promise<void> {
    const params = new HttpParams().set('year', year).set('month', month);
    await firstValueFrom(
      this.http.delete(`${this.base}/business-days`, { params })
    );
  }
}
