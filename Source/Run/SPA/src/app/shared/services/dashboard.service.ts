import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ApiResponse } from '../../models/api-response';
import { DashboardResponse } from '../../models/dashboard';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);

  async getDashboard(): Promise<DashboardResponse> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<DashboardResponse>>('/api/dashboard')
    );
    return response.data;
  }
}
