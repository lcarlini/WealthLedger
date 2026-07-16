import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatTooltipModule } from '@angular/material/tooltip';
import { firstValueFrom } from 'rxjs';

import { CashFlowScheduleService } from '../../shared/services/cashflow-schedule.service';
import { DashboardService } from '../../shared/services/dashboard.service';
import { IncomeService } from '../../shared/services/income.service';
import { MarketDataService } from '../../shared/services/market-data.service';
import {
  CashFlowItemType,
  CashFlowItemTypeLabels,
  CashFlowSource,
  SimulationMatrixResponse,
  ProposedCardInstallmentResponse,
} from '../../models/cashflow-schedule';
import { IncomeProfile, ExtraIncome, IncomeMonthPreview } from '../../models/income';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-control',
  standalone: true,
  imports: [
    FormsModule,
    CurrencyPipe,
    DecimalPipe,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatSelectModule,
    MatTableModule,
    MatSnackBarModule,
    MatProgressSpinnerModule,
    MatTabsModule,
    MatDialogModule,
    MatExpansionModule,
    MatTooltipModule,
  ],
  templateUrl: './control.component.html',
  styleUrl: './control.component.scss',
})
export class ControlComponent implements OnInit {
  private readonly service = inject(CashFlowScheduleService);
  private readonly dashboardService = inject(DashboardService);
  private readonly incomeService = inject(IncomeService);
  private readonly marketDataService = inject(MarketDataService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  loading = signal(false);
  simulation = signal<SimulationMatrixResponse | null>(null);
  proposedInstallments = signal<ProposedCardInstallmentResponse[]>([]);
  addingAll = signal(false);

  fromYear = signal(new Date().getFullYear());
  fromMonth = signal(new Date().getMonth() + 1);
  projectionYears = signal(3);
  monthCount = computed(() => this.projectionYears() * 12);
  startingBalance = signal(0);

  // ── Income ──
  incomeProfile = signal<IncomeProfile | null>(null);
  incomeForm = { hourlyRateUsd: 0, hoursPerDay: 8, usdBrlRate: 0, taxPercent: 0 };
  incomeSaving = signal(false);
  incomePreview = signal<IncomeMonthPreview[]>([]);
  extraIncomeList = signal<ExtraIncome[]>([]);
  extraForm = { year: new Date().getFullYear(), month: new Date().getMonth() + 1, amount: 0, description: '' };

  form = {
    name: '',
    itemType: CashFlowItemType.Debt,
    amountPerMonth: 0,
    startYear: new Date().getFullYear(),
    startMonth: new Date().getMonth() + 1,
    numberOfMonths: 1,
    displayOrder: 0,
  };

  itemTypes = [
    { value: CashFlowItemType.Income, label: CashFlowItemTypeLabels[CashFlowItemType.Income] },
    { value: CashFlowItemType.Expense, label: CashFlowItemTypeLabels[CashFlowItemType.Expense] },
    { value: CashFlowItemType.Debt, label: CashFlowItemTypeLabels[CashFlowItemType.Debt] },
    { value: CashFlowItemType.CardInstallment, label: CashFlowItemTypeLabels[CashFlowItemType.CardInstallment] },
  ];

  months = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
  monthNames = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'];

  yearOptions = computed(() => {
    const y = this.fromYear();
    return [y - 2, y - 1, y, y + 1, y + 2];
  });

  exportCsvUrl = computed(() => {
    const base = '/api/cashflow-schedule/export-csv';
    return `${base}?fromYear=${this.fromYear()}&fromMonth=${this.fromMonth()}&monthCount=${this.monthCount()}&startingBalance=${this.startingBalance()}`;
  });

  ngOnInit(): void {
    this.loadStartingBalance();
    this.loadSimulation();
    this.loadProposed();
    this.loadIncomeProfile();
    this.loadExtraIncome();
  }

  async loadStartingBalance(): Promise<void> {
    try {
      const dash = await this.dashboardService.getDashboard();
      this.startingBalance.set(dash.totalAmount ?? 0);
    } catch {
      // leave at 0
    }
  }

  async loadSimulation(): Promise<void> {
    this.loading.set(true);
    try {
      this.simulation.set(
        await this.service.getSimulation(this.fromYear(), this.fromMonth(), this.monthCount(), this.startingBalance())
      );
    } finally {
      this.loading.set(false);
    }
  }

  async loadProposed(): Promise<void> {
    try {
      this.proposedInstallments.set(await this.service.getProposedCardInstallments());
    } catch {
      this.proposedInstallments.set([]);
    }
  }

  async addItem(): Promise<void> {
    if (!this.form.name.trim()) {
      this.snackBar.open('Name is required.', 'Close', { duration: 3000 });
      return;
    }
    const amount = this.form.itemType === CashFlowItemType.Income
      ? this.form.amountPerMonth
      : -Math.abs(this.form.amountPerMonth);
    try {
      await this.service.create({
        name: this.form.name.trim(),
        itemType: this.form.itemType,
        amountPerMonth: amount,
        startYear: this.form.startYear,
        startMonth: this.form.startMonth,
        numberOfMonths: this.form.numberOfMonths,
        source: CashFlowSource.Manual,
        displayOrder: this.form.displayOrder,
      });
      this.snackBar.open('Item added.', 'Close', { duration: 3000 });
      this.form.name = '';
      this.form.amountPerMonth = 0;
      this.form.numberOfMonths = 1;
      this.loadSimulation();
    } catch {
      this.snackBar.open('Failed to add item.', 'Close', { duration: 3000 });
    }
  }

  async addFromCard(proposal: ProposedCardInstallmentResponse): Promise<void> {
    try {
      await this.service.addFromCard(proposal.bankTransactionId);
      this.snackBar.open('Added to schedule.', 'Close', { duration: 3000 });
      this.loadProposed();
      this.loadSimulation();
    } catch {
      this.snackBar.open('Failed to add.', 'Close', { duration: 3000 });
    }
  }

  async addAllFromCard(): Promise<void> {
    this.addingAll.set(true);
    try {
      const result = await this.service.addAllFromCard();
      this.snackBar.open(`Added ${result.added} installments to schedule.`, 'Close', { duration: 3000 });
      this.loadProposed();
      this.loadSimulation();
    } catch {
      this.snackBar.open('Failed to add installments.', 'Close', { duration: 3000 });
    } finally {
      this.addingAll.set(false);
    }
  }

  addingConsolidated = signal(false);

  async addAllFromCardConsolidated(): Promise<void> {
    this.addingConsolidated.set(true);
    try {
      const result = await this.service.addAllFromCardConsolidated();
      this.snackBar.open(`Added ${result.added} consolidated rows to schedule.`, 'Close', { duration: 3000 });
      this.loadProposed();
      this.loadSimulation();
    } catch {
      this.snackBar.open('Failed to add consolidated items.', 'Close', { duration: 3000 });
    } finally {
      this.addingConsolidated.set(false);
    }
  }

  async deleteRow(row: { id: string; name: string }): Promise<void> {
    const ref = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: { title: 'Remove item', message: `Remove "${row.name}" from the schedule?` },
    });
    const ok = await firstValueFrom(ref.afterClosed());
    if (!ok) return;
    try {
      await this.service.delete(row.id);
      this.snackBar.open('Removed.', 'Close', { duration: 3000 });
      this.loadSimulation();
    } catch {
      this.snackBar.open('Failed to remove.', 'Close', { duration: 3000 });
    }
  }

  formatAmount(value: number | null): string {
    if (value == null) return '—';
    const abs = Math.abs(value);
    const formatted = abs.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    return value < 0 ? `(${formatted})` : formatted;
  }

  applyPeriod(): void {
    const today = new Date();
    this.fromYear.set(today.getFullYear());
    this.fromMonth.set(today.getMonth() + 1);
    this.projectionYears.set(Math.max(1, Math.floor(this.projectionYears() || 1)));
    this.loadSimulation();
    this.loadIncomePreview();
  }

  // ── Income ────────────────────────────────────────────

  async loadIncomeProfile(): Promise<void> {
    try {
      const profile = await this.incomeService.getProfile();
      this.incomeProfile.set(profile);
      if (profile) {
        this.incomeForm.hourlyRateUsd = profile.hourlyRateUsd;
        this.incomeForm.hoursPerDay = profile.hoursPerDay;
        this.incomeForm.usdBrlRate = profile.usdBrlRate;
        this.incomeForm.taxPercent = profile.taxPercent;
      } else {
        // Auto-populate FX rate from market data
        try {
          const market = await this.marketDataService.getMarketData();
          if (market.usdBrl > 0) {
            this.incomeForm.usdBrlRate = Math.round(market.usdBrl * 100) / 100;
          }
        } catch { /* ignore */ }
      }
      this.loadIncomePreview();
    } catch { /* ignore */ }
  }

  async saveIncomeProfile(): Promise<void> {
    if (this.incomeForm.hourlyRateUsd <= 0) {
      this.snackBar.open('Enter an hourly rate.', 'Close', { duration: 3000 });
      return;
    }
    this.incomeSaving.set(true);
    try {
      const saved = await this.incomeService.saveProfile({
        hourlyRateUsd: this.incomeForm.hourlyRateUsd,
        hoursPerDay: this.incomeForm.hoursPerDay,
        usdBrlRate: this.incomeForm.usdBrlRate,
        taxPercent: this.incomeForm.taxPercent,
      });
      this.incomeProfile.set(saved);
      this.snackBar.open('Income profile saved.', 'Close', { duration: 3000 });
      this.loadIncomePreview();
      this.loadSimulation();
    } catch {
      this.snackBar.open('Failed to save income profile.', 'Close', { duration: 3000 });
    } finally {
      this.incomeSaving.set(false);
    }
  }

  async loadIncomePreview(): Promise<void> {
    if (!this.incomeProfile()) return;
    try {
      const res = await this.incomeService.getPreview(this.fromYear(), this.fromMonth(), this.monthCount());
      this.incomePreview.set(res.months ?? []);
    } catch {
      this.incomePreview.set([]);
    }
  }

  async loadExtraIncome(): Promise<void> {
    try {
      this.extraIncomeList.set(await this.incomeService.getExtra());
    } catch {
      this.extraIncomeList.set([]);
    }
  }

  async addExtraIncome(): Promise<void> {
    if (!this.extraForm.description.trim() || this.extraForm.amount <= 0) {
      this.snackBar.open('Fill in description and amount.', 'Close', { duration: 3000 });
      return;
    }
    try {
      await this.incomeService.addExtra({
        year: this.extraForm.year,
        month: this.extraForm.month,
        amount: this.extraForm.amount,
        description: this.extraForm.description.trim(),
      });
      this.snackBar.open('Extra income added.', 'Close', { duration: 3000 });
      this.extraForm.description = '';
      this.extraForm.amount = 0;
      this.loadExtraIncome();
      this.loadIncomePreview();
      this.loadSimulation();
    } catch {
      this.snackBar.open('Failed to add extra income.', 'Close', { duration: 3000 });
    }
  }

  async deleteExtraIncome(entry: ExtraIncome): Promise<void> {
    try {
      await this.incomeService.deleteExtra(entry.id);
      this.snackBar.open('Extra income removed.', 'Close', { duration: 3000 });
      this.loadExtraIncome();
      this.loadIncomePreview();
      this.loadSimulation();
    } catch {
      this.snackBar.open('Failed to remove.', 'Close', { duration: 3000 });
    }
  }

  isIncomeRow(itemType: string): boolean {
    return itemType === 'IncomeGross' || itemType === 'IncomeTax' || itemType === 'ExtraIncome';
  }

  // ── Business day overrides ────────────────────────────

  async onBusinessDaysChange(row: IncomeMonthPreview, newValue: number): Promise<void> {
    if (newValue < 0 || newValue > 31 || newValue === row.businessDays) return;
    try {
      await this.incomeService.setBusinessDayOverride({
        year: row.year,
        month: row.month,
        days: newValue,
      });
      this.loadIncomePreview();
      this.loadSimulation();
    } catch {
      this.snackBar.open('Failed to update business days.', 'Close', { duration: 3000 });
    }
  }

  async resetBusinessDays(row: IncomeMonthPreview): Promise<void> {
    try {
      await this.incomeService.resetBusinessDayOverride(row.year, row.month);
      this.loadIncomePreview();
      this.loadSimulation();
    } catch {
      this.snackBar.open('Failed to reset.', 'Close', { duration: 3000 });
    }
  }
}
