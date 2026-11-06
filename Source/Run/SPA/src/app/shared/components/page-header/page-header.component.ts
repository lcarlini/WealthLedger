import { Component, Input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

@Component({
  selector: 'app-page-header',
  standalone: true,
  imports: [MatIconModule, MatTooltipModule],
  template: `
    <header class="wl-page-head">
      <div class="wl-page-head-main">
        <ng-content select="[leading]" />
        @if (icon) {
          <div
            class="wl-page-icon"
            [style.--ico]="iconColor || 'rgb(var(--wl-accent-rgb))'"
            [matTooltip]="iconTooltip || title"
            matTooltipShowDelay="400"
          >
            <mat-icon>{{ icon }}</mat-icon>
          </div>
        }
        <div class="wl-page-copy">
          @if (tag) {
            <p class="wl-feature-tag">{{ tag }}</p>
          }
          <h1 class="wl-page-title">{{ title }}</h1>
          @if (lead) {
            <p class="wl-page-lead">{{ lead }}</p>
          }
          <ng-content select="[meta]" />
        </div>
      </div>
      <div class="wl-page-actions header-actions">
        <ng-content />
      </div>
    </header>
  `,
  styles: [
    `
      :host {
        display: block;
        margin-bottom: 1.15rem;
      }
    `,
  ],
})
export class PageHeaderComponent {
  @Input({ required: true }) title = '';
  @Input() lead = '';
  @Input() tag = '';
  @Input() icon = '';
  @Input() iconColor = '';
  @Input() iconTooltip = '';
}
