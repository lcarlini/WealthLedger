import { Injectable, signal } from '@angular/core';

const StorageKey = 'wealthledger-theme';

export type ThemePreference = 'light' | 'dark' | 'system';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private mq?: MediaQueryList;
  private readonly preferenceSignal = signal<ThemePreference>(this.readStored());

  readonly preference = this.preferenceSignal.asReadonly();

  init(): void {
    if (typeof window === 'undefined') return;
    this.mq = window.matchMedia('(prefers-color-scheme: dark)');
    this.mq.addEventListener('change', () => this.apply());
    this.apply();
  }

  setPreference(value: ThemePreference): void {
    this.preferenceSignal.set(value);
    try {
      localStorage.setItem(StorageKey, value);
    } catch {
      /* private mode / blocked storage */
    }
    this.apply();
  }

  private apply(): void {
    if (typeof document === 'undefined') return;
    const p = this.preferenceSignal();
    const systemDark = this.mq?.matches ?? false;
    const dark = p === 'dark' || (p === 'system' && systemDark);

    const root = document.documentElement;
    root.classList.remove('theme-light', 'theme-dark');
    root.classList.add(dark ? 'theme-dark' : 'theme-light');
    root.style.setProperty('color-scheme', dark ? 'dark' : 'light');
  }

  private readStored(): ThemePreference {
    if (typeof localStorage === 'undefined') return 'system';
    try {
      const v = localStorage.getItem(StorageKey);
      if (v === 'light' || v === 'dark' || v === 'system') return v;
    } catch {
      /* ignore */
    }
    return 'system';
  }
}
