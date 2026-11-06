import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiResponse } from '../../models/api-response';

export type AiProvider = 'local' | 'anthropic' | 'openai';

export const AI_PROVIDERS: readonly AiProvider[] = ['local', 'anthropic', 'openai'];

/** What the server reports. Secrets are never returned, only whether each one is saved. */
export interface AppSettingsView {
  aiProvider: AiProvider;
  aiModel: string | null;
  allowCloudData: boolean;
  anthropicKeyConfigured: boolean;
  openAiKeyConfigured: boolean;
  brapiTokenConfigured: boolean;
  fredKeyConfigured: boolean;
}

/** Partial update: omitted/null = unchanged; for secrets an empty string removes the saved value. */
export interface AppSettingsUpdate {
  aiProvider?: AiProvider;
  aiModel?: string;
  allowCloudData?: boolean;
  anthropicApiKey?: string;
  openAiApiKey?: string;
  brapiToken?: string;
  fredApiKey?: string;
}

@Injectable({ providedIn: 'root' })
export class AppSettingsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/settings';

  get(): Promise<AppSettingsView> {
    return this.call(this.http.get<ApiResponse<AppSettingsView>>(this.baseUrl));
  }

  update(update: AppSettingsUpdate): Promise<AppSettingsView> {
    return this.call(this.http.put<ApiResponse<AppSettingsView>>(this.baseUrl, update));
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
