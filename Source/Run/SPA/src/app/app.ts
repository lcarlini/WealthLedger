import {
  Component,
  ElementRef,
  HostListener,
  inject,
  signal,
  OnInit,
  OnDestroy,
  ViewChild,
} from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { BreakpointObserver } from '@angular/cdk/layout';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSidenav, MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatBadgeModule } from '@angular/material/badge';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Subscription } from 'rxjs';

import { TaskService } from './shared/services/task.service';
import { BillPaymentAlertService } from './shared/services/bill-payment-alert.service';
import { AppearanceService } from './shared/services/appearance.service';
import { LanguageService } from './shared/i18n/language.service';
import { TranslatePipe } from './shared/i18n/translate.pipe';
import { AppLanguage } from './shared/i18n/translations';
import { AppearancePanelComponent } from './shared/components/appearance-panel/appearance-panel.component';
import { TextSizePanelComponent } from './shared/components/text-size-panel/text-size-panel.component';

type PopoverId = 'text' | 'theme' | null;

interface NavItem {
  path: string;
  exact?: boolean;
  icon: string;
  labelKey: string;
  colorVar: string;
  badge?: 'tasks' | 'bills';
}

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
    MatTooltipModule,
    TranslatePipe,
    AppearancePanelComponent,
    TextSizePanelComponent,
  ],
  template: `
    <header class="app-toolbar">
      <button
        type="button"
        class="menu-toggle"
        (click)="toggleNav()"
        [matTooltip]="(navOpen() ? 'nav.hideMenu' : 'nav.showMenu') | t"
        [attr.aria-label]="(navOpen() ? 'nav.hideMenu' : 'nav.showMenu') | t"
        [attr.aria-expanded]="navOpen()"
      >
        <mat-icon>{{ navOpen() ? 'menu_open' : 'menu' }}</mat-icon>
      </button>

      <div class="brand">
        <img class="app-logo" src="logo.svg" alt="" width="30" height="30" />
        <span class="app-title">WealthLedger</span>
      </div>

      <span class="toolbar-spacer"></span>

      <div class="settings-bar" role="toolbar" [attr.aria-label]="'nav.settings' | t">
        <div class="lang-switch" role="group" [attr.aria-label]="'nav.language' | t">
          <button
            type="button"
            class="lang-btn"
            [class.active]="langService.lang() === 'pt'"
            (click)="setLanguage('pt')"
          >
            PT
          </button>
          <button
            type="button"
            class="lang-btn"
            [class.active]="langService.lang() === 'en'"
            (click)="setLanguage('en')"
          >
            EN
          </button>
        </div>

        <div class="settings-divider" aria-hidden="true"></div>

        <div class="popover-anchor" data-popover="text">
          <button
            type="button"
            class="settings-btn text-btn"
            [class.open]="openPopover() === 'text'"
            [attr.aria-expanded]="openPopover() === 'text'"
            aria-haspopup="dialog"
            [matTooltip]="'nav.textSize' | t"
            (click)="onTextBtn($event)"
          >
            <span class="settings-ico text-ico">T</span>
            <span class="settings-label">{{ appearance.textScale() }}%</span>
          </button>
          @if (openPopover() === 'text') {
            <div class="popover-panel" role="dialog" (click)="$event.stopPropagation()">
              <app-text-size-panel />
            </div>
          }
        </div>

        <div class="popover-anchor" data-popover="theme">
          <button
            type="button"
            class="settings-btn theme-btn"
            [class.open]="openPopover() === 'theme'"
            [attr.aria-expanded]="openPopover() === 'theme'"
            aria-haspopup="dialog"
            [matTooltip]="'nav.appearance' | t"
            (click)="onThemeBtn($event)"
          >
            <mat-icon class="settings-ico theme-ico">palette</mat-icon>
            <span class="settings-label">{{ 'nav.theme' | t }}</span>
          </button>
          @if (openPopover() === 'theme') {
            <div class="popover-panel" role="dialog" (click)="$event.stopPropagation()">
              <app-appearance-panel />
            </div>
          }
        </div>

        <a
          class="settings-btn settings-page-btn"
          routerLink="/settings"
          routerLinkActive="open"
          [matTooltip]="'nav.settingsPage' | t"
          (click)="closePopovers()"
        >
          <mat-icon class="settings-ico">settings</mat-icon>
          <span class="settings-label">{{ 'nav.settingsPage' | t }}</span>
        </a>
        <a
          class="settings-btn about-btn"
          routerLink="/about"
          routerLinkActive="open"
          [matTooltip]="'nav.about' | t"
          (click)="closePopovers()"
        >
          <mat-icon class="settings-ico about-ico">info</mat-icon>
          <span class="settings-label">{{ 'nav.about' | t }}</span>
        </a>
      </div>
    </header>

    <mat-sidenav-container class="app-sidenav-container">
      <mat-sidenav
        #sidenav
        class="app-sidenav"
        [mode]="isHandset() ? 'over' : 'side'"
        [opened]="navOpen()"
        (openedChange)="onNavOpenedChange($event)"
        [fixedInViewport]="isHandset()"
        [attr.aria-label]="'nav.menu' | t"
      >
        <div class="sidenav-head">
          <span>{{ 'nav.menu' | t }}</span>
          <button
            type="button"
            class="sidenav-close"
            (click)="closeNav()"
            [matTooltip]="'nav.hideMenu' | t"
            [attr.aria-label]="'nav.hideMenu' | t"
          >
            <mat-icon>chevron_left</mat-icon>
          </button>
        </div>

        <nav class="app-nav" aria-label="Main">
          @for (item of navItems; track item.path) {
            <a
              class="nav-item"
              [routerLink]="item.path"
              routerLinkActive="active"
              [routerLinkActiveOptions]="{ exact: !!item.exact }"
              [matTooltip]="item.labelKey | t"
              matTooltipShowDelay="350"
              matTooltipPosition="right"
              (click)="onNavClick()"
            >
              <span class="nav-ico-wrap" [style.--ico]=" 'var(' + item.colorVar + ')' ">
                <mat-icon
                  class="nav-icon"
                  [matBadge]="badgeFor(item) ?? 0"
                  [matBadgeHidden]="badgeFor(item) == null"
                  matBadgeColor="warn"
                  matBadgeSize="small"
                  >{{ item.icon }}</mat-icon
                >
              </span>
              <span class="nav-label">{{ item.labelKey | t }}</span>
            </a>
          }
        </nav>

        <footer class="app-credit">
          <a
            class="app-credit-site"
            href="https://lcarlini.github.io/WealthLedger/"
            target="_blank"
            rel="noopener noreferrer"
          >
            lcarlini.github.io/WealthLedger
          </a>
          <p class="app-credit-author">
            {{ 'nav.creditBy' | t }}
            <a
              href="https://lcarlini.github.io/lcarlini/"
              target="_blank"
              rel="noopener noreferrer"
              >Leandro Carlini Mingorance</a
            >
          </p>
        </footer>
      </mat-sidenav>

      <mat-sidenav-content class="app-content">
        <div class="route-shell wl-animate-in">
          <router-outlet />
        </div>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: [
    `
      :host {
        display: flex;
        flex-direction: column;
        height: 100vh;
        height: 100dvh;
        width: 100%;
        overflow: hidden;
        position: relative;
      }

      /* ── Top: settings only ─────────────────────────────────────────── */
      .app-toolbar {
        z-index: 40;
        flex: 0 0 auto;
        display: flex;
        align-items: center;
        gap: 0.5rem;
        min-height: 3.35rem;
        padding: 0.35rem max(0.75rem, env(safe-area-inset-right)) 0.35rem
          max(0.5rem, env(safe-area-inset-left));
        padding-top: calc(0.35rem + env(safe-area-inset-top));
        background: var(--wl-toolbar);
        color: #f8fafc;
        box-shadow: 0 2px 18px rgba(0, 0, 0, 0.35);
        border-bottom: 1px solid rgba(255, 255, 255, 0.06);
      }

      .menu-toggle {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 2.4rem;
        height: 2.4rem;
        border: 0;
        border-radius: 0.55rem;
        background: transparent;
        color: inherit;
        cursor: pointer;
        flex-shrink: 0;
        transition: background 0.2s ease;
      }

      .menu-toggle:hover {
        background: rgba(255, 255, 255, 0.12);
      }

      .brand {
        display: inline-flex;
        align-items: center;
        gap: 0.55rem;
        min-width: 0;
      }

      .app-logo {
        border-radius: 0.45rem;
        display: block;
        flex-shrink: 0;
      }

      .app-title {
        font-family: 'DM Sans', Roboto, 'Helvetica Neue', sans-serif;
        font-size: 1.1rem;
        font-weight: 700;
        letter-spacing: -0.02em;
        white-space: nowrap;
      }

      .toolbar-spacer {
        flex: 1 1 auto;
      }

      .settings-bar {
        display: inline-flex;
        align-items: center;
        gap: 0.35rem;
        flex-shrink: 0;
      }

      .settings-divider {
        width: 1px;
        height: 1.4rem;
        background: rgba(255, 255, 255, 0.22);
        margin-inline: 0.25rem;
      }

      .lang-switch {
        display: inline-flex;
        border: 1px solid rgba(255, 255, 255, 0.28);
        border-radius: 999px;
        overflow: hidden;
        background: rgba(0, 0, 0, 0.2);
      }

      .lang-btn {
        border: 0;
        background: transparent;
        color: rgba(255, 255, 255, 0.78);
        font: inherit;
        font-size: 0.7rem;
        font-weight: 750;
        letter-spacing: 0.06em;
        padding: 0.32rem 0.65rem;
        cursor: pointer;
      }

      .lang-btn.active {
        background: #fff;
        color: var(--wl-accent-strong, #0f766e);
      }

      .popover-anchor {
        position: relative;
      }

      .settings-btn {
        display: inline-flex;
        align-items: center;
        gap: 0.4rem;
        height: 2.15rem;
        padding: 0 0.75rem;
        border-radius: 999px;
        border: 1px solid rgba(255, 255, 255, 0.22);
        background: rgba(0, 0, 0, 0.18);
        color: #f8fafc;
        font: inherit;
        cursor: pointer;
        transition: border-color 0.2s ease, box-shadow 0.2s ease, background 0.2s ease;
      }

      .settings-btn:hover,
      .settings-btn.open {
        border-color: rgba(255, 255, 255, 0.5);
        box-shadow: 0 0 18px rgba(var(--wl-accent-rgb), 0.35);
        background: rgba(0, 0, 0, 0.28);
      }

      .settings-ico {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        line-height: 1;
      }

      .text-ico {
        font-weight: 800;
        font-size: 0.95rem;
        color: #67e8f9;
      }

      .theme-ico {
        font-size: 1.1rem;
        width: 1.1rem;
        height: 1.1rem;
        color: #f9a8d4;
      }

      .about-btn {
        text-decoration: none;
      }

      .about-ico {
        font-size: 1.1rem;
        width: 1.1rem;
        height: 1.1rem;
        color: #fdba74;
      }

      .settings-label {
        font-size: 0.8rem;
        font-weight: 700;
      }

      .text-btn .settings-label {
        color: #a5f3fc;
      }

      .popover-panel {
        position: absolute;
        top: calc(100% + 0.55rem);
        right: 0;
        z-index: 60;
        filter: drop-shadow(0 18px 40px rgba(0, 0, 0, 0.45));
        animation: wl-pop 0.22s cubic-bezier(0.22, 1, 0.36, 1);
      }

      @keyframes wl-pop {
        from {
          opacity: 0;
          transform: translateY(-6px) scale(0.97);
        }
        to {
          opacity: 1;
          transform: none;
        }
      }

      /* ── Left: navigation menu ──────────────────────────────────────── */
      .app-sidenav-container {
        flex: 1 1 auto;
        min-height: 0;
        width: 100%;
        background: transparent;
      }

      .app-sidenav {
        width: var(--wl-sidebar-width, 16.5rem);
        border-right: 1px solid var(--wl-border);
        background: var(--wl-sidebar-wash);
        display: flex;
        flex-direction: column;
      }

      .sidenav-head {
        flex: 0 0 auto;
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 0.5rem;
        padding: 0.85rem 0.55rem 0.45rem 1rem;
        font-size: 0.68rem;
        font-weight: 750;
        letter-spacing: 0.1em;
        text-transform: uppercase;
        color: var(--wl-text-muted);
      }

      .sidenav-close {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 2rem;
        height: 2rem;
        border: 0;
        border-radius: 0.5rem;
        background: transparent;
        color: var(--wl-text-muted);
        cursor: pointer;
      }

      .sidenav-close:hover {
        background: color-mix(in srgb, rgba(var(--wl-accent-rgb), 0.14) 100%, transparent);
        color: var(--wl-text);
      }

      .sidenav-close mat-icon {
        font-size: 1.35rem;
        width: 1.35rem;
        height: 1.35rem;
      }

      .app-nav {
        flex: 1 1 auto;
        overflow: auto;
        padding: 0.25rem 0.55rem 0.85rem;
        display: flex;
        flex-direction: column;
        gap: 0.15rem;
      }

      .nav-item {
        display: grid;
        grid-template-columns: auto 1fr;
        align-items: center;
        gap: 0.65rem;
        padding: 0.55rem 0.65rem;
        border-radius: 0.7rem;
        color: var(--wl-text);
        text-decoration: none;
        border: 1px solid transparent;
        transition: background 0.2s ease, border-color 0.2s ease, transform 0.2s ease;
      }

      .nav-item:hover {
        background: color-mix(in srgb, rgba(var(--wl-accent-rgb), 0.1) 100%, transparent);
      }

      .nav-item.active {
        background: color-mix(in srgb, rgba(var(--wl-accent-rgb), 0.18) 100%, transparent);
        border-color: rgba(var(--wl-accent-rgb), 0.35);
        box-shadow: inset 3px 0 0 rgb(var(--wl-accent-rgb));
      }

      .nav-ico-wrap {
        display: inline-grid;
        place-items: center;
        width: 1.85rem;
        height: 1.85rem;
        border-radius: 0.5rem;
        background: color-mix(in srgb, var(--ico) 16%, transparent);
        color: var(--ico);
      }

      .nav-icon {
        font-size: 1.15rem;
        width: 1.15rem;
        height: 1.15rem;
        color: inherit;
        filter: drop-shadow(0 0 6px color-mix(in srgb, currentColor 50%, transparent));
      }

      .nav-item.active .nav-ico-wrap {
        background: color-mix(in srgb, var(--ico) 26%, transparent);
        box-shadow: 0 0 14px color-mix(in srgb, var(--ico) 35%, transparent);
      }

      .nav-label {
        font-size: 0.88rem;
        font-weight: 600;
        letter-spacing: -0.01em;
        white-space: nowrap;
        overflow: hidden;
        text-overflow: ellipsis;
      }

      .app-credit {
        flex: 0 0 auto;
        padding: 0.85rem 1rem 1.1rem;
        border-top: 1px solid var(--wl-border);
        font-size: 0.72rem;
        line-height: 1.45;
        color: var(--wl-text-muted);
      }

      .app-credit-site {
        display: block;
        font-weight: 600;
        color: var(--wl-accent, #0d9488);
        text-decoration: none;
        margin-bottom: 0.5rem;
        word-break: break-all;
      }

      .app-credit-site:hover {
        text-decoration: underline;
      }

      .app-credit-author {
        margin: 0;
      }

      .app-credit-author a {
        color: var(--wl-accent-sky, #0284c7);
        font-weight: 600;
        text-decoration: none;
      }

      .app-credit-author a:hover {
        text-decoration: underline;
      }

      .app-content {
        background: var(--wl-content-wash);
        overflow: auto;
      }

      .route-shell {
        min-height: 100%;
      }

      @media (max-width: 959.98px) {
        .settings-label {
          display: none;
        }

        .settings-btn {
          padding: 0 0.55rem;
          min-width: 2.15rem;
          justify-content: center;
        }

        .app-title {
          font-size: 1rem;
        }

        .popover-panel {
          right: -0.25rem;
          max-width: calc(100vw - 1rem);
        }
      }

      @media (max-width: 480px) {
        .lang-switch {
          display: none;
        }

        .settings-divider {
          display: none;
        }

        .app-title {
          max-width: 6.5rem;
          overflow: hidden;
          text-overflow: ellipsis;
        }
      }
    `,
  ],
})
export class App implements OnInit, OnDestroy {
  @ViewChild('sidenav') sidenav!: MatSidenav;

  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly taskService = inject(TaskService);
  private readonly billAlertService = inject(BillPaymentAlertService);
  private readonly bp = inject(BreakpointObserver);
  readonly appearance = inject(AppearanceService);
  readonly langService = inject(LanguageService);

  pendingTaskCount = signal(0);
  pendingBillCount = signal(0);
  isHandset = signal(false);
  /** Left nav open/closed — user can toggle on any screen size. */
  navOpen = signal(true);
  openPopover = signal<PopoverId>(null);

  private subs = new Subscription();
  private pollId?: ReturnType<typeof setInterval>;

  readonly navItems: NavItem[] = [
    { path: '/dashboard', icon: 'dashboard', labelKey: 'nav.dashboard', colorVar: '--wl-nav-icon-dashboard' },
    { path: '/financial-institutions', icon: 'account_balance', labelKey: 'nav.institutions', colorVar: '--wl-nav-icon-bank' },
    { path: '/investments', exact: true, icon: 'savings', labelKey: 'nav.investments', colorVar: '--wl-nav-icon-savings' },
    { path: '/investments/archived', icon: 'inventory_2', labelKey: 'nav.archived', colorVar: '--wl-nav-icon-archive' },
    { path: '/portfolio', icon: 'pie_chart', labelKey: 'nav.portfolio', colorVar: '--wl-nav-icon-portfolio' },
    { path: '/trades', icon: 'swap_horiz', labelKey: 'nav.trades', colorVar: '--wl-nav-icon-portfolio' },
    { path: '/my-tasks', icon: 'task_alt', labelKey: 'nav.tasks', colorVar: '--wl-nav-icon-tasks', badge: 'tasks' },
    { path: '/bills-payments', icon: 'payments', labelKey: 'nav.bills', colorVar: '--wl-nav-icon-bills', badge: 'bills' },
    { path: '/control', icon: 'table_chart', labelKey: 'nav.control', colorVar: '--wl-nav-icon-control' },
    { path: '/import', icon: 'upload_file', labelKey: 'nav.import', colorVar: '--wl-nav-icon-import' },
    { path: '/analytics', icon: 'insights', labelKey: 'nav.analytics', colorVar: '--wl-nav-icon-analytics' },
    { path: '/advisor', icon: 'psychology', labelKey: 'nav.advisor', colorVar: '--wl-nav-icon-analytics' },
    { path: '/calculator', icon: 'calculate', labelKey: 'nav.calculator', colorVar: '--wl-nav-icon-calc' },
  ];

  @HostListener('document:click', ['$event'])
  onDocumentClick(ev: MouseEvent): void {
    if (!this.openPopover()) return;
    const target = ev.target as Node | null;
    if (target && this.host.nativeElement.contains(target)) {
      const anchor = (ev.target as HTMLElement | null)?.closest?.('.popover-anchor');
      if (anchor && this.host.nativeElement.contains(anchor)) return;
    }
    this.closePopovers();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closePopovers();
  }

  ngOnInit(): void {
    this.appearance.init();
    this.langService.setLanguage(this.langService.lang());
    this.loadPendingCounts();
    this.pollId = setInterval(() => this.loadPendingCounts(), 30000);

    this.subs.add(
      this.bp.observe('(max-width: 959.98px)').subscribe((state) => {
        const wasHandset = this.isHandset();
        this.isHandset.set(state.matches);
        // Default: open on desktop, closed as overlay on phone.
        if (state.matches && !wasHandset) {
          this.navOpen.set(false);
        } else if (!state.matches && wasHandset) {
          this.navOpen.set(true);
        }
      }),
    );
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    if (this.pollId) clearInterval(this.pollId);
  }

  toggleNav(): void {
    this.navOpen.update((open) => !open);
    this.closePopovers();
  }

  closeNav(): void {
    this.navOpen.set(false);
    this.closePopovers();
  }

  onNavOpenedChange(opened: boolean): void {
    this.navOpen.set(opened);
  }

  onTextBtn(event: MouseEvent): void {
    event.stopPropagation();
    this.openPopover.update((cur) => (cur === 'text' ? null : 'text'));
  }

  onThemeBtn(event: MouseEvent): void {
    event.stopPropagation();
    this.openPopover.update((cur) => (cur === 'theme' ? null : 'theme'));
  }

  closePopovers(): void {
    this.openPopover.set(null);
  }

  badgeFor(item: NavItem): number | null {
    if (item.badge === 'tasks') {
      const n = this.pendingTaskCount();
      return n > 0 ? n : null;
    }
    if (item.badge === 'bills') {
      const n = this.pendingBillCount();
      return n > 0 ? n : null;
    }
    return null;
  }

  onNavClick(): void {
    this.closePopovers();
    if (this.isHandset()) {
      this.navOpen.set(false);
    }
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
