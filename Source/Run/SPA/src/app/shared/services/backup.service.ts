import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiResponse } from '../../models/api-response';

export interface BackupSettings {
  enabled: boolean;
  intervalDays: number;
  maxBackups: number;
}

export interface BackupInfo {
  fileName: string;
  createdUtc: string;
  sizeBytes: number;
  reason: 'manual' | 'scheduled' | 'pre-upgrade' | 'pre-restore' | 'pre-import' | string;
}

export interface BackupStatus {
  settings: BackupSettings;
  databasePath: string;
  backupDirectory: string;
  backupCount: number;
  lastBackupUtc: string | null;
  nextScheduledBackupUtc: string | null;
  restorePending: boolean;
}

export const BACKUP_LIMITS = {
  intervalDaysMin: 1,
  intervalDaysMax: 365,
  maxBackupsMin: 1,
  maxBackupsMax: 100,
} as const;

@Injectable({ providedIn: 'root' })
export class BackupService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/backups';

  getStatus(): Promise<BackupStatus> {
    return this.call(this.http.get<ApiResponse<BackupStatus>>(`${this.baseUrl}/status`));
  }

  list(): Promise<BackupInfo[]> {
    return this.call(this.http.get<ApiResponse<BackupInfo[]>>(this.baseUrl)).then(items => items ?? []);
  }

  updateSettings(settings: BackupSettings): Promise<BackupSettings> {
    return this.call(this.http.put<ApiResponse<BackupSettings>>(`${this.baseUrl}/settings`, settings));
  }

  createBackup(): Promise<BackupInfo> {
    return this.call(this.http.post<ApiResponse<BackupInfo>>(this.baseUrl, {}));
  }

  restore(fileName: string): Promise<BackupStatus> {
    return this.call(this.http.post<ApiResponse<BackupStatus>>(`${this.baseUrl}/${encodeURIComponent(fileName)}/restore`, {}));
  }

  remove(fileName: string): Promise<BackupStatus> {
    return this.call(this.http.delete<ApiResponse<BackupStatus>>(`${this.baseUrl}/${encodeURIComponent(fileName)}`));
  }

  /** Asks the local server to open the backup folder in the OS file manager. */
  openFolder(): Promise<BackupStatus> {
    return this.call(this.http.post<ApiResponse<BackupStatus>>(`${this.baseUrl}/open-folder`, {}));
  }

  downloadUrl(fileName: string): string {
    return `${this.baseUrl}/${encodeURIComponent(fileName)}/download`;
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
