import { Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  AppearanceService,
  AccentId,
  ThemeId,
} from '../../services/appearance.service';
import { TranslatePipe } from '../../i18n/translate.pipe';

@Component({
  selector: 'app-appearance-panel',
  standalone: true,
  imports: [MatIconModule, MatTooltipModule, TranslatePipe],
  template: `
    <div class="wl-panel" role="dialog" [attr.aria-label]="'nav.appearance' | t">
      <header class="wl-panel-head">
        <h2>{{ 'nav.appearance' | t }}</h2>
        <p>{{ 'nav.remembered' | t }}</p>
      </header>

      <section class="wl-panel-section">
        <span class="wl-panel-label">{{ 'nav.theme' | t }}</span>
        <div class="wl-option-stack" role="radiogroup">
          @for (opt of themes; track opt.id) {
            <button
              type="button"
              class="wl-option-row"
              role="radio"
              [attr.aria-checked]="appearance.theme() === opt.id"
              [class.active]="appearance.theme() === opt.id"
              (click)="appearance.setTheme(opt.id)"
            >
              <mat-icon [style.color]="opt.color">{{ opt.icon }}</mat-icon>
              <span>{{ opt.labelKey | t }}</span>
            </button>
          }
        </div>
      </section>

      <section class="wl-panel-section">
        <span class="wl-panel-label">{{ 'nav.accent' | t }}</span>
        <div class="wl-swatches" role="radiogroup">
          @for (id of accents; track id) {
            <button
              type="button"
              class="wl-swatch"
              role="radio"
              [attr.aria-checked]="appearance.accent() === id"
              [class.active]="appearance.accent() === id"
              [style.--swatch]="appearance.accentHex[id]"
              [matTooltip]="id"
              (click)="appearance.setAccent(id)"
            ></button>
          }
        </div>
      </section>

      <section class="wl-panel-section">
        <span class="wl-panel-label">{{ 'nav.density' | t }}</span>
        <div class="wl-seg" role="radiogroup">
          <button type="button" role="radio"
                  [attr.aria-checked]="appearance.density() === 'comfortable'"
                  [class.active]="appearance.density() === 'comfortable'"
                  (click)="appearance.setDensity('comfortable')">
            {{ 'nav.densityComfortable' | t }}
          </button>
          <button type="button" role="radio"
                  [attr.aria-checked]="appearance.density() === 'compact'"
                  [class.active]="appearance.density() === 'compact'"
                  (click)="appearance.setDensity('compact')">
            {{ 'nav.densityCompact' | t }}
          </button>
        </div>
      </section>

      <div class="wl-panel-row">
        <section class="wl-panel-section">
          <span class="wl-panel-label">{{ 'nav.contrast' | t }}</span>
          <div class="wl-seg" role="radiogroup">
            <button type="button" role="radio"
                    [attr.aria-checked]="appearance.contrast() === 'standard'"
                    [class.active]="appearance.contrast() === 'standard'"
                    (click)="appearance.setContrast('standard')">
              {{ 'nav.contrastStandard' | t }}
            </button>
            <button type="button" role="radio"
                    [attr.aria-checked]="appearance.contrast() === 'more'"
                    [class.active]="appearance.contrast() === 'more'"
                    (click)="appearance.setContrast('more')">
              {{ 'nav.contrastMore' | t }}
            </button>
          </div>
        </section>

        <section class="wl-panel-section">
          <span class="wl-panel-label">{{ 'nav.motion' | t }}</span>
          <div class="wl-seg" role="radiogroup">
            <button type="button" role="radio"
                    [attr.aria-checked]="appearance.motion() === 'on'"
                    [class.active]="appearance.motion() === 'on'"
                    (click)="appearance.setMotion('on')">
              {{ 'nav.motionOn' | t }}
            </button>
            <button type="button" role="radio"
                    [attr.aria-checked]="appearance.motion() === 'off'"
                    [class.active]="appearance.motion() === 'off'"
                    (click)="appearance.setMotion('off')">
              {{ 'nav.motionOff' | t }}
            </button>
          </div>
        </section>
      </div>
    </div>
  `,
  styles: [
    `
      :host {
        display: block;
        width: min(22rem, 92vw);
      }
    `,
  ],
})
export class AppearancePanelComponent {
  readonly appearance = inject(AppearanceService);

  readonly themes: { id: ThemeId; icon: string; color: string; labelKey: string }[] = [
    { id: 'blue', icon: 'dark_mode', color: '#60a5fa', labelKey: 'nav.themeBlue' },
    { id: 'white', icon: 'light_mode', color: '#fbbf24', labelKey: 'nav.themeWhite' },
    { id: 'black', icon: 'contrast', color: '#e2e8f0', labelKey: 'nav.themeBlack' },
  ];

  readonly accents: AccentId[] = ['blue', 'teal', 'purple', 'orange', 'pink'];
}
