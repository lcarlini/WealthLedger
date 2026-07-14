import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ApiResponse } from '../../models/api-response';
import { PagedResponse } from '../../models/paged-response';
import { FinancialInstitution, FinancialInstitutionRequest } from '../../models/financial-institution';
import { CsvImportResult } from '../../models/csv-import-result';

@Injectable({ providedIn: 'root' })
export class FinancialInstitutionService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/financial-institutions';

  getExportCsvUrl(): string {
    return `${this.baseUrl}/export-csv`;
  }

  getImportCsvTemplateUrl(): string {
    return `${this.baseUrl}/import-csv-template`;
  }

  async importCsv(file: File): Promise<CsvImportResult> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    const response = await firstValueFrom(
      this.http.post<ApiResponse<CsvImportResult>>(`${this.baseUrl}/import-csv`, formData)
    );
    return response.data ?? {
      rowsTotal: 0,
      created: 0,
      updated: 0,
      skipped: 0,
      failed: 0,
      rowErrors: [],
    };
  }

  async getAll(): Promise<FinancialInstitution[]> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<FinancialInstitution[]>>(`${this.baseUrl}/all`)
    );
    return response.data ?? [];
  }

  async getPaged(page = 1, pageSize = 50, search?: string): Promise<PagedResponse<FinancialInstitution>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    if (search) {
      params = params.set('search', search);
    }

    const response = await firstValueFrom(
      this.http.get<ApiResponse<PagedResponse<FinancialInstitution>>>(this.baseUrl, { params })
    );
    return (
      response.data ?? {
        page,
        pageSize,
        totalCount: 0,
        items: [],
      }
    );
  }

  async getById(id: string): Promise<FinancialInstitution> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<FinancialInstitution>>(`${this.baseUrl}/${id}`)
    );
    return response.data;
  }

  async create(body: FinancialInstitutionRequest): Promise<FinancialInstitution> {
    const response = await firstValueFrom(
      this.http.post<ApiResponse<FinancialInstitution>>(this.baseUrl, body)
    );
    return response.data;
  }

  async update(id: string, body: FinancialInstitutionRequest): Promise<FinancialInstitution> {
    const response = await firstValueFrom(
      this.http.put<ApiResponse<FinancialInstitution>>(`${this.baseUrl}/${id}`, body)
    );
    return response.data;
  }

  async delete(id: string): Promise<void> {
    await firstValueFrom(
      this.http.delete(`${this.baseUrl}/${id}`)
    );
  }
}
