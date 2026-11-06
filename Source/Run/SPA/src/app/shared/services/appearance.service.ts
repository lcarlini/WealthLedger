import { Injectable, computed, signal } from '@angular/core';

const StorageKey = 'wealthledger-appearance-v2';
const LegacyThemeKey = 'wealthledger-theme';

export type ThemeId = 'blue' | 'white' | 'black';
export type AccentId = 'blue' | 'teal' | 'purple' | 'orange' | 'pink';
export type DensityId = 'comfortable' | 'compact';
export type ContrastId = 'standard' | 'more';
export type MotionId = 'on' | 'off';

/** @deprecated Prefer ThemeId — kept for migration of old preference. */
export type ThemePreference = 'light' | 'dark' | 'system';

export interface AppearanceState {
  theme: ThemeId;
  accent: AccentId;
  density: DensityId;
  contrast: ContrastId;
  motion: MotionId;
  textScale: number;
  autoscale: boolean;
}

const DEFAULTS: AppearanceState = {
  theme: 'blue',
  accent: 'teal',
  density: 'comfortable',
  contrast: 'standard',
  motion: 'on',
  textScale: 100,
  autoscale: true,
};

const ACCENT_HEX: Record<AccentId, string> = {
  blue: '#3b82f6',
  teal: '#14b8a6',
  purple: '#a855f7',
  orange: '#f97316',
  pink: '#ec4899',
};

const TEXT_PRESETS = [80, 90, 100, 115, 130, 150] as const;

@Injectable({ providedIn: 'root' })
export class AppearanceService {
  private readonly stateSignal = signal<AppearanceState>(this.readStored());
  private resizeBound = false;

  readonly state = this.stateSignal.asReadonly();
  readonly theme = computed(() => this.stateSignal().theme);
  readonly accent = computed(() => this.stateSignal().accent);
  readonly density = computed(() => this.stateSignal().density);
  readonly contrast = computed(() => this.stateSignal().contrast);
  readonly motion = computed(() => this.stateSignal().motion);
  readonly textScale = computed(() => this.stateSignal().textScale);
  readonly autoscale = computed(() => this.stateSignal().autoscale);
  readonly textPresets = TEXT_PRESETS;
  readonly accentHex = ACCENT_HEX;

  /** Back-compat for old ThemeService API used elsewhere. */
  readonly preference = computed<ThemePreference>(() => {
    switch (this.stateSignal().theme) {
      case 'white':
        return 'light';
      case 'black':
        return 'dark';
      default:
        return 'dark';
    }
  });

  init(): void {
    if (typeof window === 'undefined') return;
    if (!this.resizeBound) {
      this.resizeBound = true;
      window.addEventListener('resize', () => this.applyScale(), { passive: true });
    }
    this.apply();
  }

  setTheme(theme: ThemeId): void {
    this.patch({ theme });
  }

  setAccent(accent: AccentId): void {
    this.patch({ accent });
  }

  setDensity(density: DensityId): void {
    this.patch({ density });
  }

  setContrast(contrast: ContrastId): void {
    this.patch({ contrast });
  }

  setMotion(motion: MotionId): void {
    this.patch({ motion });
  }

  setTextScale(scale: number): void {
    this.patch({ textScale: this.clampScale(scale) });
  }

  bumpTextScale(delta: number): void {
    this.setTextScale(this.stateSignal().textScale + delta);
  }

  setAutoscale(autoscale: boolean): void {
    this.patch({ autoscale });
  }

  /** @deprecated Prefer setTheme. Maps old light/dark/system to new themes. */
  setPreference(value: ThemePreference): void {
    const theme: ThemeId =
      value === 'light' ? 'white' : value === 'dark' ? 'black' : 'blue';
    this.setTheme(theme);
  }

  private patch(partial: Partial<AppearanceState>): void {
    this.stateSignal.update((s) => ({ ...s, ...partial }));
    this.persist();
    this.apply();
  }

  private apply(): void {
    if (typeof document === 'undefined') return;
    const s = this.stateSignal();
    const root = document.documentElement;

    root.classList.remove(
      'theme-light',
      'theme-dark',
      'theme-blue',
      'theme-white',
      'theme-black',
    );
    root.classList.add(`theme-${s.theme}`);
    // Material still keys off light/dark
    root.classList.add(s.theme === 'white' ? 'theme-light' : 'theme-dark');
    root.style.setProperty('color-scheme', s.theme === 'white' ? 'light' : 'dark');

    root.dataset['theme'] = s.theme;
    root.dataset['accent'] = s.accent;
    root.dataset['density'] = s.density;
    root.dataset['contrast'] = s.contrast;
    root.dataset['motion'] = s.motion;

    this.applyAccent(s.accent, s.theme);
    this.applyScale();

    const meta = document.querySelector('meta[name="theme-color"]');
    if (meta) {
      const colors: Record<ThemeId, string> = {
        blue: '#0b1629',
        white: '#f8fafc',
        black: '#000000',
      };
      // White theme: tint the browser chrome with the accent
      meta.setAttribute(
        'content',
        s.theme === 'white' ? ACCENT_HEX[s.accent] : colors[s.theme],
      );
    }
  }

  /** Push accent into CSS vars + Material primary tokens used by every screen. */
  private applyAccent(accent: AccentId, theme: ThemeId): void {
    const root = document.documentElement;
    const hex = ACCENT_HEX[accent];
    const rgb = this.hexToRgb(hex);
    const strong = this.mixHex(hex, '#000000', theme === 'white' ? 0.28 : 0.22);
    const soft = this.mixHex(hex, theme === 'white' ? '#ffffff' : '#0b1629', 0.72);
    const muted = this.mixHex(hex, theme === 'white' ? '#ffffff' : '#94a3b8', 0.45);

    root.style.setProperty('--wl-accent', hex);
    root.style.setProperty('--wl-accent-rgb', rgb);
    root.style.setProperty('--wl-accent-strong', strong);
    root.style.setProperty('--wl-accent-soft', soft);
    root.style.setProperty('--wl-accent-muted', muted);

    // Angular Material 3 system tokens → whole app (buttons, tabs, focus, etc.)
    root.style.setProperty('--mat-sys-primary', hex);
    root.style.setProperty('--mat-sys-on-primary', '#ffffff');
    root.style.setProperty('--mat-sys-primary-container', soft);
    root.style.setProperty(
      '--mat-sys-on-primary-container',
      theme === 'white' ? strong : '#ffffff',
    );
    root.style.setProperty('--mat-sys-inverse-primary', muted);

    // Toolbar follows accent so the chrome is not stuck on teal
    if (theme === 'white') {
      root.style.setProperty(
        '--wl-toolbar',
        `linear-gradient(90deg, ${strong} 0%, ${hex} 55%, ${this.mixHex(hex, '#0369a1', 0.35)} 100%)`,
      );
    } else if (theme === 'black') {
      root.style.setProperty(
        '--wl-toolbar',
        `linear-gradient(90deg, #000 0%, ${this.mixHex(hex, '#000000', 0.75)} 50%, #000 100%)`,
      );
    } else {
      root.style.setProperty(
        '--wl-toolbar',
        `linear-gradient(90deg, ${this.mixHex(hex, '#0a1a33', 0.65)} 0%, ${this.mixHex(hex, '#0d2244', 0.55)} 50%, #0b1c38 100%)`,
      );
    }

    root.style.setProperty(
      '--wl-title-gradient',
      theme === 'white'
        ? `linear-gradient(90deg, ${strong}, ${hex} 50%, ${this.mixHex(hex, '#7c3aed', 0.35)})`
        : `linear-gradient(90deg, ${hex}, ${this.mixHex(hex, '#38bdf8', 0.4)} 48%, ${this.mixHex(hex, '#a78bfa', 0.45)})`,
    );
  }

  private applyScale(): void {
    if (typeof document === 'undefined' || typeof window === 'undefined') return;
    const s = this.stateSignal();
    let scale = s.textScale;

    if (s.autoscale) {
      const w = window.innerWidth;
      // Tiny phones → shrink a bit; huge projectors → grow toward readable size
      let autoBoost = 0;
      if (w < 360) autoBoost = -10;
      else if (w < 480) autoBoost = -5;
      else if (w >= 2560) autoBoost = 20;
      else if (w >= 1920) autoBoost = 10;
      else if (w >= 1440) autoBoost = 5;
      scale = this.clampScale(s.textScale + autoBoost);
    }

    const rem = (16 * scale) / 100;
    document.documentElement.style.fontSize = `${rem}px`;
    document.documentElement.style.setProperty('--wl-text-scale', String(scale));
  }

  private clampScale(n: number): number {
    return Math.min(180, Math.max(70, Math.round(n / 5) * 5));
  }

  private persist(): void {
    try {
      localStorage.setItem(StorageKey, JSON.stringify(this.stateSignal()));
    } catch {
      /* private mode */
    }
  }

  private readStored(): AppearanceState {
    if (typeof localStorage === 'undefined') return { ...DEFAULTS };
    try {
      const raw = localStorage.getItem(StorageKey);
      if (raw) {
        const parsed = JSON.parse(raw) as Partial<AppearanceState>;
        return {
          ...DEFAULTS,
          ...parsed,
          textScale: this.clampScale(parsed.textScale ?? DEFAULTS.textScale),
        };
      }
      // Migrate legacy theme preference
      const legacy = localStorage.getItem(LegacyThemeKey);
      if (legacy === 'light') return { ...DEFAULTS, theme: 'white' };
      if (legacy === 'dark') return { ...DEFAULTS, theme: 'black' };
      if (legacy === 'system') return { ...DEFAULTS, theme: 'blue' };
    } catch {
      /* ignore */
    }
    return { ...DEFAULTS };
  }

  private hexToRgb(hex: string): string {
    const [r, g, b] = this.hexParts(hex);
    return `${r}, ${g}, ${b}`;
  }

  private hexParts(hex: string): [number, number, number] {
    const h = hex.replace('#', '');
    const n = parseInt(h.length === 3 ? h.split('').map((c) => c + c).join('') : h, 16);
    return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
  }

  /** Mix two hex colors; amount is weight of `withHex` (0..1). */
  private mixHex(hex: string, withHex: string, amount: number): string {
    const a = Math.min(1, Math.max(0, amount));
    const [r1, g1, b1] = this.hexParts(hex);
    const [r2, g2, b2] = this.hexParts(withHex);
    const r = Math.round(r1 * (1 - a) + r2 * a);
    const g = Math.round(g1 * (1 - a) + g2 * a);
    const b = Math.round(b1 * (1 - a) + b2 * a);
    return `#${[r, g, b].map((v) => v.toString(16).padStart(2, '0')).join('')}`;
  }
}

/** Alias so existing ThemeService imports keep compiling during transition. */
export { AppearanceService as ThemeService };
