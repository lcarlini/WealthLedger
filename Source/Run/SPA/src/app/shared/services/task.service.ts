import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ApiResponse } from '../../models/api-response';
import { TaskItem, FutureTask } from '../../models/task-item';

@Injectable({ providedIn: 'root' })
export class TaskService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/tasks';

  async getPending(): Promise<TaskItem[]> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<TaskItem[]>>(`${this.baseUrl}/pending`)
    );
    return response.data ?? [];
  }

  async complete(id: string): Promise<TaskItem> {
    const response = await firstValueFrom(
      this.http.put<ApiResponse<TaskItem>>(`${this.baseUrl}/${id}/complete`, {})
    );
    return response.data;
  }

  async getCompleted(): Promise<TaskItem[]> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<TaskItem[]>>(`${this.baseUrl}/completed`)
    );
    return response.data ?? [];
  }

  async getFuture(monthsAhead = 12): Promise<FutureTask[]> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<FutureTask[]>>(`${this.baseUrl}/future?monthsAhead=${monthsAhead}`)
    );
    return response.data ?? [];
  }

  async getPendingCount(): Promise<number> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<number>>(`${this.baseUrl}/pending-count`)
    );
    return response.data ?? 0;
  }
}
