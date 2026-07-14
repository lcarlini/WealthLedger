import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ApiResponse } from '../../models/api-response';
import {
  StatementImportResponse,
  BankTransactionResponse,
} from '../../models/statement-import';

@Injectable({ providedIn: 'root' })
export class OfxImportService {
  private readonly http = inject(HttpClient);

  async uploadOfx(file: File, source: 'Card' | 'Checking'): Promise<StatementImportResponse> {
    const formData = new FormData();
    formData.append('file', file);
    const sourceNum = source === 'Card' ? 1 : 2;
    const response = await firstValueFrom(
      this.http.post<ApiResponse<StatementImportResponse>>(
        `/api/import/ofx?source=${sourceNum}`,
        formData
      )
    );
    return response.data;
  }

  async getImports(): Promise<StatementImportResponse[]> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<StatementImportResponse[]>>('/api/import/ofx')
    );
    return response.data ?? [];
  }

  async getTransactions(importId: string): Promise<BankTransactionResponse[]> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<BankTransactionResponse[]>>(
        `/api/import/ofx/${importId}/transactions`
      )
    );
    return response.data ?? [];
  }

  async deleteImport(importId: string): Promise<void> {
    await firstValueFrom(
      this.http.delete(`/api/import/ofx/${importId}`)
    );
  }
}
