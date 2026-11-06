import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import { LanguageService } from '../../shared/i18n/language.service';
import { TranslatePipe } from '../../shared/i18n/translate.pipe';
import {
  BUDGET_LIMITS,
  BudgetItemStatus,
  BudgetLevel,
  BudgetService,
  BudgetStatus,
} from '../../shared/services/budget.service';

@Component({
  selector: 'app-budgets',
  standalone: true,
  imports: [
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    PageHeaderComponent,
    TranslatePipe,
  ],
  templateUrl: './budgets.component.html',
  styleUrl: './budgets.component.scss',
})
export class BudgetsComponent implements OnInit {
  private readonly api = inject(BudgetService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  readonly lang = inject(LanguageService);

  readonly limits = BUDGET_LIMITS;
  readonly status = signal<BudgetStatus | null>(null);
  readonly categories = signal<string[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  /** Month picker value, `yyyy-MM` (what <input type="month"> uses). */
  month = BudgetsComponent.currentMonth();

  // Form model.
  category = '';
  monthlyLimit: number | null = null;

  readonly totalPercent = computed(() => {
    const s = this.status();
    return s && s.totalLimit > 0 ? Math.round((s.totalSpent / s.totalLimit) * 100) : 0;
  });

  ngOnInit(): void {
    void this.loadCategories();
    void this.refresh();
  }

  /** Local date, not UTC: late evening in Brazil must not jump to next month. */
  static currentMonth(now = new Date()): string {
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
  }

  private parsedMonth(): { year: number; month: number } | null {
    const match = /^(\d{4})-(\d{2})$/.exec(this.month);
    return match ? { year: Number(match[1]), month: Number(match[2]) } : null;
  }

  async refresh(): Promise<void> {
    const ym = this.parsedMonth();
    if (!ym) return;
    this.loading.set(true);
    try {
      this.status.set(await this.api.status(ym.year, ym.month));
      this.error.set(null);
    } catch (err) {
      this.error.set(this.messageOf(err));
    } finally {
      this.loading.set(false);
    }
  }

  private async loadCategories(): Promise<void> {
    try {
      this.categories.set(await this.api.categories());
    } catch {
      /* suggestions are optional */
    }
  }

  isValid(): boolean {
    return this.category.trim().length > 0
      && this.category.trim().length <= this.limits.categoryMaxLength
      && this.monthlyLimit !== null
      && this.monthlyLimit > 0
      && this.monthlyLimit <= this.limits.limitMax;
  }

  edit(item: BudgetItemStatus): void {
    this.category = item.category;
    this.monthlyLimit = item.monthlyLimit;
    document.getElementById('budget-category')?.focus();
  }

  /** Pre-fills the form for a category that is being spent on but has no budget yet. */
  suggest(category: string, amount: number): void {
    this.category = category;
    // Round the current month's spending up to the next 50 as a starting point.
    this.monthlyLimit = Math.max(50, Math.ceil(amount / 50) * 50);
    document.getElementById('budget-limit')?.focus();
  }

  async save(): Promise<void> {
    if (!this.isValid()) return;
    await this.run(async () => {
      await this.api.save(this.category.trim(), this.monthlyLimit as number);
      this.category = '';
      this.monthlyLimit = null;
      await this.refresh();
      this.toast(this.lang.t('budgets.saved'));
    });
  }

  async remove(item: BudgetItemStatus): Promise<void> {
    const confirmed = await this.confirm({
      title: this.lang.t('budgets.deleteTitle'),
      message: `${this.lang.t('budgets.deleteMessage')} ${item.category}`,
      confirmLabel: this.lang.t('budgets.delete'),
      cancelLabel: this.lang.t('settings.cancel'),
      destructive: true,
    });
    if (!confirmed) return;
    await this.run(async () => {
      await this.api.remove(item.id);
      await this.refresh();
      this.toast(this.lang.t('budgets.deleted'));
    });
  }

  levelKey(level: BudgetLevel): string {
    return `budgets.level.${level}`;
  }

  brl(value: number): string {
    return new Intl.NumberFormat(this.lang.lang() === 'pt' ? 'pt-BR' : 'en-US', {
      style: 'currency',
      currency: 'BRL',
    }).format(value);
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch (err) {
      this.toast(this.messageOf(err));
    } finally {
      this.busy.set(false);
    }
  }

  private async confirm(data: ConfirmDialogData): Promise<boolean> {
    const ref = this.dialog.open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
      data,
      width: '440px',
    });
    return (await firstValueFrom(ref.afterClosed())) === true;
  }

  private toast(message: string): void {
    this.snackBar.open(message, undefined, { duration: 4000 });
  }

  private messageOf(err: unknown): string {
    return err instanceof Error ? err.message : String(err);
  }
}
