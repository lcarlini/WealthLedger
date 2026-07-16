import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ApiResponse } from '../../models/api-response';
import {
  CalendarEventItem,
  InvestmentGoal,
  PassiveIncome,
  PortfolioOverview,
  PortfolioSnapshot,
  WatchlistItem,
} from '../../models/portfolio';

@Injectable({ providedIn: 'root' })
export class PortfolioService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/portfolio';

  async getOverview(projectionYears = 3): Promise<PortfolioOverview> {
    const params = new HttpParams().set('projectionYears', projectionYears);
    const res = await firstValueFrom(
      this.http.get<ApiResponse<PortfolioOverview>>(`${this.base}/overview`, { params })
    );
    return res.data;
  }

  async getCalendar(monthsAhead = 12): Promise<CalendarEventItem[]> {
    const params = new HttpParams().set('monthsAhead', monthsAhead);
    const res = await firstValueFrom(
      this.http.get<ApiResponse<CalendarEventItem[]>>(`${this.base}/calendar`, { params })
    );
    return res.data ?? [];
  }

  async getSnapshots(): Promise<PortfolioSnapshot[]> {
    const res = await firstValueFrom(
      this.http.get<ApiResponse<PortfolioSnapshot[]>>(`${this.base}/snapshots`)
    );
    return res.data ?? [];
  }

  async captureSnapshot(notes?: string): Promise<PortfolioSnapshot> {
    const params = notes ? new HttpParams().set('notes', notes) : undefined;
    const res = await firstValueFrom(
      this.http.post<ApiResponse<PortfolioSnapshot>>(`${this.base}/snapshots`, {}, { params })
    );
    return res.data;
  }

  getExportJsonUrl(): string {
    return `${this.base}/export-json`;
  }

  async importJson(file: File): Promise<void> {
    const form = new FormData();
    form.append('file', file, file.name);
    await firstValueFrom(this.http.post(`${this.base}/import-json`, form));
  }
}

@Injectable({ providedIn: 'root' })
export class PassiveIncomeApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/passive-income';

  async getAll(): Promise<PassiveIncome[]> {
    const res = await firstValueFrom(this.http.get<ApiResponse<PassiveIncome[]>>(this.base));
    return res.data ?? [];
  }

  async create(body: Partial<PassiveIncome>): Promise<PassiveIncome> {
    const res = await firstValueFrom(this.http.post<ApiResponse<PassiveIncome>>(this.base, body));
    return res.data;
  }

  async update(id: string, body: Partial<PassiveIncome>): Promise<PassiveIncome> {
    const res = await firstValueFrom(this.http.put<ApiResponse<PassiveIncome>>(`${this.base}/${id}`, body));
    return res.data;
  }

  async delete(id: string): Promise<void> {
    await firstValueFrom(this.http.delete(`${this.base}/${id}`));
  }
}

@Injectable({ providedIn: 'root' })
export class GoalApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/goals';

  async getAll(): Promise<InvestmentGoal[]> {
    const res = await firstValueFrom(this.http.get<ApiResponse<InvestmentGoal[]>>(this.base));
    return res.data ?? [];
  }

  async create(body: Partial<InvestmentGoal>): Promise<InvestmentGoal> {
    const res = await firstValueFrom(this.http.post<ApiResponse<InvestmentGoal>>(this.base, body));
    return res.data;
  }

  async update(id: string, body: Partial<InvestmentGoal>): Promise<InvestmentGoal> {
    const res = await firstValueFrom(this.http.put<ApiResponse<InvestmentGoal>>(`${this.base}/${id}`, body));
    return res.data;
  }

  async delete(id: string): Promise<void> {
    await firstValueFrom(this.http.delete(`${this.base}/${id}`));
  }
}

@Injectable({ providedIn: 'root' })
export class WatchlistApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/watchlist';

  async getAll(): Promise<WatchlistItem[]> {
    const res = await firstValueFrom(this.http.get<ApiResponse<WatchlistItem[]>>(this.base));
    return res.data ?? [];
  }

  async create(body: Partial<WatchlistItem>): Promise<WatchlistItem> {
    const res = await firstValueFrom(this.http.post<ApiResponse<WatchlistItem>>(this.base, body));
    return res.data;
  }

  async delete(id: string): Promise<void> {
    await firstValueFrom(this.http.delete(`${this.base}/${id}`));
  }
}
