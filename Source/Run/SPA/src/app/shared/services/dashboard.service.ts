import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ApiResponse } from '../../models/api-response';
import { DashboardResponse } from '../../models/dashboard';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);

  async getDashboard(projectionYears = 3): Promise<DashboardResponse> {
    const params = new HttpParams().set('projectionYears', Math.max(1, Math.floor(projectionYears)));
    const response = await firstValueFrom(
      this.http.get<ApiResponse<DashboardResponse>>('/api/dashboard', { params })
    );
    return response.data;
  }
}
