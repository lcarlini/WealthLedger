import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { firstValueFrom, Observable } from 'rxjs';
import { ApiResponse } from '../../models/api-response';

export type AdvisorScope = 'portfolio' | 'expenses' | 'general';

export const ADVISOR_SCOPES: readonly AdvisorScope[] = ['portfolio', 'expenses', 'general'];

export interface AdvisorTurn {
  role: 'user' | 'assistant';
  content: string;
}

export interface AdvisorRequest {
  question: string;
  scope: AdvisorScope;
  language: 'pt' | 'en';
  history?: AdvisorTurn[];
}

export interface AdvisorAnswer {
  answer: string;
  provider: string;
  model: string;
  /** True when aggregated portfolio/spending figures were attached to the request. */
  usedUserData: boolean;
}

/** Prompt material for the in-browser model. Aggregated figures only; it never leaves this machine. */
export interface AdvisorContext {
  systemPrompt: string;
  dataSummary: string;
}

export const ADVISOR_LIMITS = { questionMaxLength: 2000 } as const;

@Injectable({ providedIn: 'root' })
export class AdvisorService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/advisor';

  /** Asks the cloud provider chosen in Settings (through the server, which holds the key). */
  ask(request: AdvisorRequest): Promise<AdvisorAnswer> {
    return this.call(this.http.post<ApiResponse<AdvisorAnswer>>(`${this.baseUrl}/ask`, request));
  }

  context(scope: AdvisorScope, language: 'pt' | 'en'): Promise<AdvisorContext> {
    return this.call(
      this.http.get<ApiResponse<AdvisorContext>>(`${this.baseUrl}/context`, { params: { scope, language } }),
    );
  }

  /** Checks the saved key and model with a request that carries no personal data. */
  test(): Promise<AdvisorAnswer> {
    return this.call(this.http.post<ApiResponse<AdvisorAnswer>>(`${this.baseUrl}/test`, {}));
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
