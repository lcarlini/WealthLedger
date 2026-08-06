import { Component, computed, inject, signal, OnInit } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatBadgeModule } from '@angular/material/badge';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';

import { TaskService } from './shared/services/task.service';
import { BillPaymentAlertService } from './shared/services/bill-payment-alert.service';
import { ThemeService, ThemePreference } from './shared/services/theme.service';
import { LanguageService } from './shared/i18n/language.service';
import { TranslatePipe } from './shared/i18n/translate.pipe';
import { AppLanguage } from './shared/i18n/translations';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatIconModule,
    MatButtonModule,
    MatSidenavModule,
    MatListModule,
    MatBadgeModule,
    MatMenuModule,
    MatTooltipModule,
    TranslatePipe,
  ],
  template: `
    <mat-toolbar color="primary" class="app-toolbar">
      <img class="app-logo" src="logo.svg" alt="WealthLedger" width="32" height="32" />
      <span class="app-title">WealthLedger</span>
      <span class="toolbar-spacer"></span>

      <div class="lang-switch" role="group" [attr.aria-label]="'nav.language' | t">
        <button type="button" class="lang-btn" [class.active]="langService.lang() === 'pt'"
                (click)="setLanguage('pt')">PT</button>
        <button type="button" class="lang-btn" [class.active]="langService.lang() === 'en'"
                (click)="setLanguage('en')">EN</button>
      </div>

      <button mat-icon-button type="button" [matMenuTriggerFor]="themeMenu"
              [matTooltip]="'nav.appearance' | t">
        <mat-icon>{{ themeMenuIcon() }}</mat-icon>
      </button>
      <mat-menu #themeMenu="matMenu">
        <button mat-menu-item type="button" (click)="setTheme('light')">
          <mat-icon>light_mode</mat-icon>
          <span>{{ 'nav.themeLight' | t }}</span>
        </button>
        <button mat-menu-item type="button" (click)="setTheme('dark')">
          <mat-icon>dark_mode</mat-icon>
          <span>{{ 'nav.themeDark' | t }}</span>
        </button>
        <button mat-menu-item type="button" (click)="setTheme('system')">
          <mat-icon>brightness_auto</mat-icon>
          <span>{{ 'nav.themeSystem' | t }}</span>
        </button>
      </mat-menu>
    </mat-toolbar>

    <mat-sidenav-container class="app-sidenav-container">
      <mat-sidenav mode="side" opened class="app-sidenav">
        <mat-nav-list class="app-nav-list">
          <a mat-list-item routerLink="/dashboard" routerLinkActive="active-link">
            <mat-icon matListItemIcon>dashboard</mat-icon>
            <span matListItemTitle>{{ 'nav.dashboard' | t }}</span>
          </a>
          <a mat-list-item routerLink="/financial-institutions" routerLinkActive="active-link">
            <mat-icon matListItemIcon>account_balance</mat-icon>
            <span matListItemTitle>{{ 'nav.institutions' | t }}</span>
          </a>
          <a mat-list-item routerLink="/investments" routerLinkActive="active-link"
             [routerLinkActiveOptions]="{ exact: true }">
            <mat-icon matListItemIcon>savings</mat-icon>
            <span matListItemTitle>{{ 'nav.investments' | t }}</span>
          </a>
          <a mat-list-item routerLink="/investments/archived" routerLinkActive="active-link">
            <mat-icon matListItemIcon>inventory_2</mat-icon>
            <span matListItemTitle>{{ 'nav.archived' | t }}</span>
          </a>
          <a mat-list-item routerLink="/portfolio" routerLinkActive="active-link">
            <mat-icon matListItemIcon>pie_chart</mat-icon>
            <span matListItemTitle>{{ 'nav.portfolio' | t }}</span>
          </a>
          <a mat-list-item routerLink="/my-tasks" routerLinkActive="active-link">
            <mat-icon matListItemIcon
                      [matBadge]="pendingTaskCount() > 0 ? pendingTaskCount() : null"
                      matBadgeColor="warn"
                      matBadgeSize="small">task_alt</mat-icon>
            <span matListItemTitle>{{ 'nav.tasks' | t }}</span>
          </a>
          <a mat-list-item routerLink="/bills-payments" routerLinkActive="active-link">
            <mat-icon matListItemIcon
                      [matBadge]="pendingBillCount() > 0 ? pendingBillCount() : null"
                      matBadgeColor="warn"
                      matBadgeSize="small">payments</mat-icon>
            <span matListItemTitle>{{ 'nav.bills' | t }}</span>
          </a>
          <a mat-list-item routerLink="/control" routerLinkActive="active-link">
            <mat-icon matListItemIcon>table_chart</mat-icon>
            <span matListItemTitle>{{ 'nav.control' | t }}</span>
          </a>
          <a mat-list-item routerLink="/import" routerLinkActive="active-link">
            <mat-icon matListItemIcon>upload_file</mat-icon>
            <span matListItemTitle>{{ 'nav.import' | t }}</span>
          </a>
          <a mat-list-item routerLink="/analytics" routerLinkActive="active-link">
            <mat-icon matListItemIcon>insights</mat-icon>
            <span matListItemTitle>{{ 'nav.analytics' | t }}</span>
          </a>
          <a mat-list-item routerLink="/calculator" routerLinkActive="active-link">
            <mat-icon matListItemIcon>calculate</mat-icon>
            <span matListItemTitle>{{ 'nav.calculator' | t }}</span>
          </a>
          <a mat-list-item routerLink="/about" routerLinkActive="active-link">
            <mat-icon matListItemIcon>info</mat-icon>
            <span matListItemTitle>{{ 'nav.about' | t }}</span>
          </a>
        </mat-nav-list>
        <footer class="app-credit">
          <a class="app-credit-site" href="https://lcarlini.github.io/WealthLedger/" target="_blank" rel="noopener noreferrer">
            lcarlini.github.io/WealthLedger
          </a>
          <p class="app-credit-author">
            {{ 'nav.creditBy' | t }}
            <a href="https://lcarlini.github.io/lcarlini/" target="_blank" rel="noopener noreferrer">Leandro Carlini Mingorance</a>
          </p>
        </footer>
      </mat-sidenav>

      <mat-sidenav-content class="app-content">
        <router-outlet />
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: [`
    :host {
      display: flex;
      flex-direction: column;
      height: 100vh;
      width: 100%;
      overflow: hidden;
    }

    .app-toolbar {
      z-index: 3;
      flex: 0 0 auto;
      position: relative;
      gap: 8px;
      min-height: 56px;
      background: linear-gradient(90deg, #0f766e 0%, #0e7490 55%, #0369a1 100%) !important;
      box-shadow: 0 2px 12px color-mix(in srgb, #0f766e 35%, transparent);
    }

    .app-logo {
      margin-right: 12px;
      border-radius: 8px;
      display: block;
    }

    .app-title {
      font-family: 'DM Sans', Roboto, 'Helvetica Neue', sans-serif;
      font-size: 1.25rem;
      font-weight: 650;
      letter-spacing: -0.02em;
    }

    .toolbar-spacer {
      flex: 1 1 auto;
    }

    .lang-switch {
      display: inline-flex;
      border: 1px solid color-mix(in srgb, white 40%, transparent);
      border-radius: 999px;
      overflow: hidden;
      background: color-mix(in srgb, black 18%, transparent);
    }

    .lang-btn {
      border: 0;
      background: transparent;
      color: color-mix(in srgb, white 80%, transparent);
      font: inherit;
      font-size: 0.75rem;
      font-weight: 750;
      letter-spacing: 0.06em;
      padding: 0.35rem 0.75rem;
      cursor: pointer;
      min-width: 42px;
    }

    .lang-btn.active {
      background: white;
      color: #0f766e;
    }

    .app-sidenav-container {
      flex: 1 1 auto;
      min-height: 0;
      width: 100%;
    }

    .app-sidenav {
      width: var(--wl-sidebar-width, 248px);
      border-right: 1px solid var(--mat-sys-outline-variant);
      background:
        linear-gradient(180deg, color-mix(in srgb, #0d9488 10%, transparent), transparent 28%),
        var(--mat-sys-surface-container-low);
      display: flex;
      flex-direction: column;
    }

    .app-nav-list {
      flex: 1 1 auto;
      overflow: auto;
      padding-top: 8px;
    }

    .app-credit {
      flex: 0 0 auto;
      padding: 14px 16px 18px;
      border-top: 1px solid var(--mat-sys-outline-variant);
      font-size: 0.72rem;
      line-height: 1.45;
      color: var(--mat-sys-on-surface-variant);
    }

    .app-credit-site {
      display: block;
      font-weight: 600;
      color: var(--wl-accent-teal, #0d9488);
      text-decoration: none;
      margin-bottom: 8px;
      word-break: break-all;
    }

    .app-credit-site:hover { text-decoration: underline; }

    .app-credit-author { margin: 0; }

    .app-credit-author a {
      color: var(--wl-accent-sky, #0284c7);
      font-weight: 600;
      text-decoration: none;
    }

    .app-credit-author a:hover { text-decoration: underline; }

    .app-content {
      background:
        radial-gradient(900px 420px at 100% -10%, color-mix(in srgb, #0ea5e9 12%, transparent), transparent 55%),
        radial-gradient(700px 380px at 0% 100%, color-mix(in srgb, #10b981 10%, transparent), transparent 50%),
        var(--mat-sys-surface);
      overflow: auto;
    }

    .active-link {
      background: color-mix(in srgb, #0d9488 18%, transparent) !important;
      color: #5eead4 !important;
      border-right: 3px solid #14b8a6;
    }
  `],
})
export class App implements OnInit {
  private readonly taskService = inject(TaskService);
  private readonly billAlertService = inject(BillPaymentAlertService);
  private readonly themeService = inject(ThemeService);
  readonly langService = inject(LanguageService);

  pendingTaskCount = signal(0);
  pendingBillCount = signal(0);

  readonly themeMenuIcon = computed(() => {
    switch (this.themeService.preference()) {
      case 'dark':
        return 'dark_mode';
      case 'light':
        return 'light_mode';
      default:
        return 'brightness_auto';
    }
  });

  ngOnInit(): void {
    this.themeService.init();
    this.langService.setLanguage(this.langService.lang());
    this.loadPendingCounts();
    setInterval(() => this.loadPendingCounts(), 30000);
  }

  setTheme(value: ThemePreference): void {
    this.themeService.setPreference(value);
  }

  setLanguage(value: AppLanguage): void {
    if (value === 'pt' || value === 'en') {
      this.langService.setLanguage(value);
    }
  }

  private async loadPendingCounts(): Promise<void> {
    try {
      const [tasks, bills] = await Promise.all([
        this.taskService.getPendingCount(),
        this.billAlertService.getPendingCount(),
      ]);
      this.pendingTaskCount.set(tasks);
      this.pendingBillCount.set(bills);
    } catch {
      // silently ignore
    }
  }
}
