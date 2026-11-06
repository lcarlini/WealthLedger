import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LanguageService } from '../../shared/i18n/language.service';
import { TranslatePipe } from '../../shared/i18n/translate.pipe';
import { BrowserAiService, extractAiErrorMessage } from '../../core/services/browser-ai.service';
import {
  ADVISOR_LIMITS,
  ADVISOR_SCOPES,
  AdvisorScope,
  AdvisorService,
  AdvisorTurn,
} from '../../shared/services/advisor.service';
import { AppSettingsService, AppSettingsView } from '../../shared/services/app-settings.service';

interface ChatMessage extends AdvisorTurn {
  /** Small caption under an answer: which model answered and whether personal figures were attached. */
  meta?: string;
}

/** Suggested questions per scope; the texts live in the translation tables. */
const SUGGESTIONS: Record<AdvisorScope, readonly string[]> = {
  portfolio: ['advisor.prompt.portfolio.1', 'advisor.prompt.portfolio.2', 'advisor.prompt.portfolio.3'],
  expenses: ['advisor.prompt.expenses.1', 'advisor.prompt.expenses.2', 'advisor.prompt.expenses.3'],
  general: ['advisor.prompt.general.1', 'advisor.prompt.general.2'],
};

@Component({
  selector: 'app-advisor',
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    PageHeaderComponent,
    TranslatePipe,
  ],
  templateUrl: './advisor.component.html',
  styleUrl: './advisor.component.scss',
})
export class AdvisorComponent implements OnInit {
  private readonly advisor = inject(AdvisorService);
  private readonly browserAi = inject(BrowserAiService);
  private readonly settingsApi = inject(AppSettingsService);
  readonly lang = inject(LanguageService);

  readonly scopes = ADVISOR_SCOPES;
  readonly limits = ADVISOR_LIMITS;
  readonly settings = signal<AppSettingsView | null>(null);
  readonly messages = signal<ChatMessage[]>([]);
  readonly scope = signal<AdvisorScope>('portfolio');
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly localProgress = this.browserAi.initProgress;
  readonly localLoading = this.browserAi.initializing;

  input = '';

  readonly provider = computed(() => this.settings()?.aiProvider ?? 'local');
  readonly suggestions = computed(() => SUGGESTIONS[this.scope()]);

  /** A cloud provider is selected but its key is not saved: sending would fail, so say so up front. */
  readonly missingKey = computed(() => {
    const s = this.settings();
    if (!s) return false;
    return (s.aiProvider === 'anthropic' && !s.anthropicKeyConfigured)
      || (s.aiProvider === 'openai' && !s.openAiKeyConfigured);
  });

  ngOnInit(): void {
    void this.loadSettings();
  }

  async loadSettings(): Promise<void> {
    try {
      this.settings.set(await this.settingsApi.get());
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : String(err));
    }
  }

  scopeKey(scope: AdvisorScope): string {
    return `advisor.scope.${scope}`;
  }

  setScope(scope: AdvisorScope): void {
    this.scope.set(scope);
  }

  providerKey(): string {
    return `settings.ai.provider.${this.provider()}`;
  }

  clear(): void {
    this.messages.set([]);
    this.error.set(null);
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
      event.preventDefault();
      void this.send();
    }
  }

  async send(suggestion?: string): Promise<void> {
    const question = (suggestion ?? this.input).trim();
    if (!question || this.busy() || question.length > this.limits.questionMaxLength) return;

    const history = this.messages().map(({ role, content }) => ({ role, content }));
    this.messages.update(list => [...list, { role: 'user', content: question }]);
    this.input = '';
    this.error.set(null);
    this.busy.set(true);

    try {
      const language = this.lang.lang();
      if (this.provider() === 'local') {
        // The browser model gets the aggregated figures directly; nothing is sent to a third party.
        const context = await this.advisor.context(this.scope(), language);
        const answer = await this.browserAi.chat(
          `${context.systemPrompt}\n\n${context.dataSummary}`,
          history,
          question,
        );
        this.push(answer, this.lang.t('advisor.meta.local'));
      } else {
        const result = await this.advisor.ask({ question, scope: this.scope(), language, history });
        const data = this.lang.t(result.usedUserData ? 'advisor.meta.withData' : 'advisor.meta.noData');
        this.push(result.answer, `${result.provider} · ${result.model} · ${data}`);
      }
    } catch (err) {
      this.error.set(this.provider() === 'local' ? extractAiErrorMessage(err) : this.messageOf(err));
    } finally {
      this.busy.set(false);
    }
  }

  private push(content: string, meta: string): void {
    this.messages.update(list => [...list, { role: 'assistant', content, meta }]);
  }

  private messageOf(err: unknown): string {
    return err instanceof Error ? err.message : String(err);
  }
}
