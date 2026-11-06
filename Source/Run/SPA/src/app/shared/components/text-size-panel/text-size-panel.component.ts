import { Component, inject } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { AppearanceService } from '../../services/appearance.service';
import { TranslatePipe } from '../../i18n/translate.pipe';

@Component({
  selector: 'app-text-size-panel',
  standalone: true,
  imports: [MatIconModule, MatButtonModule, TranslatePipe],
  template: `
    <div class="wl-panel" role="dialog" [attr.aria-label]="'nav.textSize' | t">
      <header class="wl-panel-head">
        <h2>{{ 'nav.textSize' | t }}</h2>
        <p>{{ 'nav.textSizeHint' | t }}</p>
      </header>

      <div class="wl-scale-control">
        <button type="button" class="wl-scale-btn" (click)="appearance.bumpTextScale(-5)"
                [attr.aria-label]="'nav.textSmaller' | t">
          <mat-icon>remove</mat-icon>
        </button>
        <div class="wl-scale-value">
          <strong>{{ appearance.textScale() }}%</strong>
          <span>{{ 'nav.ofDefault' | t }}</span>
        </div>
        <button type="button" class="wl-scale-btn" (click)="appearance.bumpTextScale(5)"
                [attr.aria-label]="'nav.textLarger' | t">
          <mat-icon>add</mat-icon>
        </button>
      </div>

      <section class="wl-panel-section">
        <span class="wl-panel-label">{{ 'nav.presets' | t }}</span>
        <div class="wl-presets">
          @for (p of appearance.textPresets; track p) {
            <button
              type="button"
              class="wl-preset"
              [class.active]="appearance.textScale() === p"
              (click)="appearance.setTextScale(p)"
            >
              {{ p }}%
            </button>
          }
        </div>
      </section>

      <section class="wl-panel-section">
        <span class="wl-panel-label">{{ 'nav.autoscale' | t }}</span>
        <div class="wl-seg" role="radiogroup">
          <button type="button" role="radio"
                  [attr.aria-checked]="appearance.autoscale()"
                  [class.active]="appearance.autoscale()"
                  (click)="appearance.setAutoscale(true)">
            {{ 'nav.motionOn' | t }}
          </button>
          <button type="button" role="radio"
                  [attr.aria-checked]="!appearance.autoscale()"
                  [class.active]="!appearance.autoscale()"
                  (click)="appearance.setAutoscale(false)">
            {{ 'nav.motionOff' | t }}
          </button>
        </div>
      </section>
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
export class TextSizePanelComponent {
  readonly appearance = inject(AppearanceService);
}