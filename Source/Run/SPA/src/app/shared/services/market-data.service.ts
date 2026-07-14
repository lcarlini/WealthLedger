import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ApiResponse } from '../../models/api-response';
import { MarketDataResponse } from '../../models/market-data';

@Injectable({ providedIn: 'root' })
export class MarketDataService {
  private readonly http = inject(HttpClient);

  async getMarketData(): Promise<MarketDataResponse> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<MarketDataResponse>>('/api/market-data')
    );
    return response.data;
  }
}
