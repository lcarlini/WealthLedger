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
import { ThemeService, ThemePreference } from './shared/services/theme.service';

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
  ],
  template: `
    <mat-toolbar color="primary" class="app-toolbar">
      <img class="app-logo" src="logo.svg" alt="WealthLedger" width="32" height="32" />
      <span class="app-title">WealthLedger</span>
      <span class="toolbar-spacer"></span>
      <button mat-icon-button type="button" [matMenuTriggerFor]="themeMenu" matTooltip="Appearance">
        <mat-icon>{{ themeMenuIcon() }}</mat-icon>
      </button>
      <mat-menu #themeMenu="matMenu">
        <button mat-menu-item type="button" (click)="setTheme('light')">
          <mat-icon>light_mode</mat-icon>
          <span>Light</span>
        </button>
        <button mat-menu-item type="button" (click)="setTheme('dark')">
          <mat-icon>dark_mode</mat-icon>
          <span>Dark</span>
        </button>
        <button mat-menu-item type="button" (click)="setTheme('system')">
          <mat-icon>brightness_auto</mat-icon>
          <span>System</span>
        </button>
      </mat-menu>
    </mat-toolbar>

    <mat-sidenav-container class="app-sidenav-container">
      <mat-sidenav mode="side" opened class="app-sidenav">
        <mat-nav-list class="app-nav-list">
          <a mat-list-item routerLink="/dashboard" routerLinkActive="active-link">
            <mat-icon matListItemIcon>dashboard</mat-icon>
            <span matListItemTitle>Dashboard</span>
          </a>
          <a mat-list-item routerLink="/financial-institutions" routerLinkActive="active-link">
            <mat-icon matListItemIcon>account_balance</mat-icon>
            <span matListItemTitle>Institutions</span>
          </a>
          <a mat-list-item routerLink="/investments" routerLinkActive="active-link">
            <mat-icon matListItemIcon>savings</mat-icon>
            <span matListItemTitle>Investments</span>
          </a>
          <a mat-list-item routerLink="/my-tasks" routerLinkActive="active-link">
            <mat-icon matListItemIcon
                      [matBadge]="pendingTaskCount() > 0 ? pendingTaskCount() : null"
                      matBadgeColor="warn"
                      matBadgeSize="small">task_alt</mat-icon>
            <span matListItemTitle>My Tasks</span>
          </a>
          <a mat-list-item routerLink="/control" routerLinkActive="active-link">
            <mat-icon matListItemIcon>table_chart</mat-icon>
            <span matListItemTitle>Control</span>
          </a>
          <a mat-list-item routerLink="/import" routerLinkActive="active-link">
            <mat-icon matListItemIcon>upload_file</mat-icon>
            <span matListItemTitle>Import OFX</span>
          </a>
          <a mat-list-item routerLink="/analytics" routerLinkActive="active-link">
            <mat-icon matListItemIcon>insights</mat-icon>
            <span matListItemTitle>Analytics</span>
          </a>
          <a mat-list-item routerLink="/calculator" routerLinkActive="active-link">
            <mat-icon matListItemIcon>calculate</mat-icon>
            <span matListItemTitle>Calculator</span>
          </a>
        </mat-nav-list>
        <footer class="app-credit">
          <a class="app-credit-site" href="https://lcarlini.github.io/WealthLedger/" target="_blank" rel="noopener noreferrer">
            lcarlini.github.io/WealthLedger
          </a>
          <p class="app-credit-author">
            Implemented by Computer Engineer
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
    }

    .app-toolbar {
      z-index: 2;
      position: relative;
      box-shadow: 0 1px 0 color-mix(in srgb, var(--mat-sys-outline-variant) 80%, transparent);
    }

    .app-logo {
      margin-right: 12px;
      border-radius: 8px;
      display: block;
    }

    .app-title {
      font-family: 'DM Sans', Roboto, 'Helvetica Neue', sans-serif;
      font-size: 1.25rem;
      font-weight: 600;
      letter-spacing: -0.02em;
    }

    .toolbar-spacer {
      flex: 1 1 auto;
    }

    .app-sidenav-container {
      flex: 1;
    }

    .app-sidenav {
      width: 240px;
      border-right: 1px solid var(--mat-sys-outline-variant);
      background: var(--mat-sys-surface-container-low);
      display: flex;
      flex-direction: column;
    }

    .app-nav-list {
      flex: 1 1 auto;
      overflow: auto;
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
      color: var(--mat-sys-primary);
      text-decoration: none;
      margin-bottom: 8px;
      word-break: break-all;
    }

    .app-credit-site:hover {
      text-decoration: underline;
    }

    .app-credit-author {
      margin: 0;
    }

    .app-credit-author a {
      color: var(--mat-sys-primary);
      font-weight: 600;
      text-decoration: none;
    }

    .app-credit-author a:hover {
      text-decoration: underline;
    }

    .app-content {
      background: var(--mat-sys-surface);
    }

    .active-link {
      background: color-mix(in srgb, var(--mat-sys-primary) 14%, transparent) !important;
      color: var(--mat-sys-primary) !important;
    }
  `],
})
export class App implements OnInit {
  private readonly taskService = inject(TaskService);
  private readonly themeService = inject(ThemeService);

  pendingTaskCount = signal(0);

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
    this.loadPendingCount();
    setInterval(() => this.loadPendingCount(), 30000);
  }

  setTheme(value: ThemePreference): void {
    this.themeService.setPreference(value);
  }

  private async loadPendingCount(): Promise<void> {
    try {
      this.pendingTaskCount.set(await this.taskService.getPendingCount());
    } catch {
      // silently ignore
    }
  }
}
