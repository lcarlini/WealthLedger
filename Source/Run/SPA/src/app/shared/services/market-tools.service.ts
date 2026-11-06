import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiResponse } from '../../models/api-response';

export interface TreasuryBond {
  type: string;
  maturity: string;
  buyRatePercent: number;
  sellRatePercent: number;
  buyPrice: number;
  sellPrice: number;
}

export interface TreasurySnapshot {
  baseDate: string;
  bonds: TreasuryBond[];
  fetchedAtUtc: string;
  fromCache: boolean;
}

export interface MarketIndex {
  symbol: string;
  name: string;
  currency: string;
  price: number;
  dayChangePercent: number;
  periodChangePercent: number;
  /** Closing values, oldest first. */
  points: number[];
}

export interface MarketIndicesSnapshot {
  indices: MarketIndex[];
  fetchedAtUtc: string;
  fromCache: boolean;
}

@Injectable({ providedIn: 'root' })
export class MarketToolsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/market-tools';

  treasury(): Promise<TreasurySnapshot> {
    return this.call(this.http.get<ApiResponse<TreasurySnapshot>>(`${this.baseUrl}/treasury`));
  }

  indices(): Promise<MarketIndicesSnapshot> {
    return this.call(this.http.get<ApiResponse<MarketIndicesSnapshot>>(`${this.baseUrl}/indices`));
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

/** Builds the `points` attribute of an SVG polyline that fits `width` x `height`. Empty for fewer than 2 values. */
export function sparklinePoints(values: readonly number[], width: number, height: number, pad = 2): string {
  if (values.length < 2) return '';
  const min = Math.min(...values);
  const max = Math.max(...values);
  const span = max - min || 1;
  const step = (width - pad * 2) / (values.length - 1);
  return values
    .map((v, i) => {
      const x = pad + i * step;
      const y = height - pad - ((v - min) / span) * (height - pad * 2);
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(' ');
}
