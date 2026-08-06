import { Injectable, computed, signal } from '@angular/core';
import { AppLanguage, TRANSLATIONS, TranslationKey } from './translations';

const StorageKey = 'wealthledger-lang';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly langSignal = signal<AppLanguage>(this.readStored());

  readonly lang = this.langSignal.asReadonly();
  readonly isPt = computed(() => this.langSignal() === 'pt');

  setLanguage(lang: AppLanguage): void {
    this.langSignal.set(lang);
    try {
      localStorage.setItem(StorageKey, lang);
    } catch {
      /* ignore */
    }
    if (typeof document !== 'undefined') {
      document.documentElement.lang = lang === 'pt' ? 'pt-BR' : 'en';
    }
  }

  t(key: TranslationKey | string, fallback?: string): string {
    const lang = this.langSignal();
    const table = TRANSLATIONS[lang] as Record<string, string>;
    const en = TRANSLATIONS.en as Record<string, string>;
    return table[key] ?? en[key] ?? fallback ?? key;
  }

  private readStored(): AppLanguage {
    if (typeof localStorage !== 'undefined') {
      try {
        const v = localStorage.getItem(StorageKey);
        if (v === 'pt' || v === 'en') return v;
      } catch {
        /* ignore */
      }
    }
    if (typeof navigator !== 'undefined' && navigator.language?.toLowerCase().startsWith('pt')) {
      return 'pt';
    }
    return 'en';
  }
}
