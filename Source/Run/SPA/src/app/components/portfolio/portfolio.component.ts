import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe, DecimalPipe, PercentPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatChipsModule } from '@angular/material/chips';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration } from 'chart.js';

import { AccountType, AccountTypeLabels, Currency } from '../../models/investment';
import {
  GoalType,
  GoalTypeLabels,
  InvestmentGoal,
  PassiveIncome,
  PassiveIncomeType,
  PassiveIncomeTypeLabels,
  PortfolioOverview,
  WatchlistItem,
} from '../../models/portfolio';
import {
  GoalApiService,
  PassiveIncomeApiService,
  PortfolioService,
  WatchlistApiService,
} from '../../shared/services/portfolio.service';
import { InvestmentService } from '../../shared/services/investment.service';

@Component({
  selector: 'app-portfolio',
  standalone: true,
  imports: [
    FormsModule,
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    PercentPipe,
    MatCardModule,
    MatIconModule,
    MatTabsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatProgressBarModule,
    MatSnackBarModule,
    MatTooltipModule,
    MatChipsModule,
    BaseChartDirective,
  ],
  templateUrl: './portfolio.component.html',
  styleUrl: './portfolio.component.scss',
})
export class PortfolioComponent implements OnInit {
  private readonly portfolioApi = inject(PortfolioService);
  private readonly passiveApi = inject(PassiveIncomeApiService);
  private readonly goalApi = inject(GoalApiService);
  private readonly watchlistApi = inject(WatchlistApiService);
  private readonly investmentApi = inject(InvestmentService);
  private readonly snackBar = inject(MatSnackBar);

  loading = signal(true);
  overview = signal<PortfolioOverview | null>(null);
  projectionYears = signal(3);
  passiveIncomes = signal<PassiveIncome[]>([]);
  goals = signal<InvestmentGoal[]>([]);
  watchlist = signal<WatchlistItem[]>([]);
  investments = signal<{ id: string; name: string }[]>([]);

  readonly exportJsonUrl = this.portfolioApi.getExportJsonUrl();
  readonly passiveTypes = Object.values(PassiveIncomeType).filter((v) => typeof v === 'number') as PassiveIncomeType[];
  readonly passiveLabels = PassiveIncomeTypeLabels;
  readonly goalTypes = Object.values(GoalType).filter((v) => typeof v === 'number') as GoalType[];
  readonly goalLabels = GoalTypeLabels;
  readonly accountTypes = [
    AccountType.Stock, AccountType.FII, AccountType.ETF, AccountType.BDR, AccountType.Crypto, AccountType.InvestmentFund,
  ];
  readonly accountTypeLabels = AccountTypeLabels;

  allocationChart = signal<ChartConfiguration<'doughnut'>['data']>({ labels: [], datasets: [] });
  timelineChart = signal<ChartConfiguration<'line'>['data']>({ labels: [], datasets: [] });
  benchmarkChart = signal<ChartConfiguration<'bar'>['data']>({ labels: [], datasets: [] });

  doughnutOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true,
    plugins: { legend: { position: 'bottom' } },
  };
  lineOptions: ChartConfiguration<'line'>['options'] = {
    responsive: true,
    plugins: { legend: { position: 'bottom' } },
    scales: { y: { beginAtZero: false } },
  };
  barOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    plugins: { legend: { position: 'bottom' } },
    scales: { y: { beginAtZero: true } },
  };

  passiveForm = {
    name: '',
    incomeType: PassiveIncomeType.Dividend,
    currency: Currency.BRL,
    amount: 0,
    paymentDate: new Date().toISOString().slice(0, 10),
    investmentId: '' as string | '',
    notes: '',
  };

  goalForm = {
    name: '',
    goalType: GoalType.NetWorth,
    currency: Currency.BRL,
    targetAmount: 100000,
    currentAmount: 0,
    targetDate: '',
    monthlyContribution: 1000,
    expectedAnnualReturnPercent: 10,
    notes: '',
  };

  watchForm = {
    ticker: '',
    name: '',
    accountType: AccountType.Stock,
    alertAbove: null as number | null,
    alertBelow: null as number | null,
  };

  projectionEndDate = computed(() => {
    const today = new Date();
    return new Date(today.getFullYear() + this.projectionYears(), today.getMonth(), today.getDate());
  });

  ngOnInit(): void {
    void this.reloadAll();
  }

  async reloadAll(): Promise<void> {
    this.loading.set(true);
    try {
      const [overview, passive, goals, watch, inv] = await Promise.all([
        this.portfolioApi.getOverview(this.projectionYears()),
        this.passiveApi.getAll().catch(() => []),
        this.goalApi.getAll().catch(() => []),
        this.watchlistApi.getAll().catch(() => []),
        this.investmentApi.getAll().catch(() => []),
      ]);
      this.overview.set(overview);
      this.passiveIncomes.set(passive);
      this.goals.set(goals);
      this.watchlist.set(watch);
      this.investments.set(inv.map((i) => ({ id: i.id, name: i.name })));
      this.buildCharts(overview);
    } catch {
      this.snackBar.open('Failed to load portfolio analytics.', 'Close', { duration: 4000 });
    } finally {
      this.loading.set(false);
    }
  }

  setProjectionYears(value: number): void {
    this.projectionYears.set(Math.max(1, Math.floor(Number(value) || 1)));
    void this.reloadAll();
  }

  async captureSnapshot(): Promise<void> {
    try {
      await this.portfolioApi.captureSnapshot();
      this.snackBar.open('Net worth snapshot saved.', 'Close', { duration: 3000 });
      await this.reloadAll();
    } catch {
      this.snackBar.open('Failed to capture snapshot.', 'Close', { duration: 3000 });
    }
  }

  async onImportJson(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    try {
      await this.portfolioApi.importJson(file);
      this.snackBar.open('JSON import completed.', 'Close', { duration: 4000 });
      await this.reloadAll();
    } catch {
      this.snackBar.open('JSON import failed.', 'Close', { duration: 4000 });
    }
  }

  async addPassive(): Promise<void> {
    if (!this.passiveForm.name.trim() || this.passiveForm.amount <= 0) {
      this.snackBar.open('Name and amount are required.', 'Close', { duration: 3000 });
      return;
    }
    try {
      await this.passiveApi.create({
        name: this.passiveForm.name,
        incomeType: this.passiveForm.incomeType,
        currency: this.passiveForm.currency,
        amount: this.passiveForm.amount,
        paymentDate: new Date(this.passiveForm.paymentDate).toISOString(),
        investmentId: this.passiveForm.investmentId || undefined,
        notes: this.passiveForm.notes || undefined,
      });
      this.passiveForm.name = '';
      this.passiveForm.amount = 0;
      this.snackBar.open('Passive income recorded.', 'Close', { duration: 3000 });
      await this.reloadAll();
    } catch {
      this.snackBar.open('Failed to save passive income.', 'Close', { duration: 3000 });
    }
  }

  async deletePassive(id: string): Promise<void> {
    await this.passiveApi.delete(id);
    await this.reloadAll();
  }

  async addGoal(): Promise<void> {
    if (!this.goalForm.name.trim() || this.goalForm.targetAmount <= 0) {
      this.snackBar.open('Name and target amount are required.', 'Close', { duration: 3000 });
      return;
    }
    try {
      await this.goalApi.create({
        name: this.goalForm.name,
        goalType: this.goalForm.goalType,
        currency: this.goalForm.currency,
        targetAmount: this.goalForm.targetAmount,
        currentAmount: this.goalForm.currentAmount,
        targetDate: this.goalForm.targetDate ? new Date(this.goalForm.targetDate).toISOString() : undefined,
        monthlyContribution: this.goalForm.monthlyContribution,
        expectedAnnualReturnPercent: this.goalForm.expectedAnnualReturnPercent,
        notes: this.goalForm.notes || undefined,
        isCompleted: false,
      });
      this.goalForm.name = '';
      this.snackBar.open('Goal created.', 'Close', { duration: 3000 });
      await this.reloadAll();
    } catch {
      this.snackBar.open('Failed to save goal.', 'Close', { duration: 3000 });
    }
  }

  async deleteGoal(id: string): Promise<void> {
    await this.goalApi.delete(id);
    await this.reloadAll();
  }

  async addWatch(): Promise<void> {
    if (!this.watchForm.ticker.trim() || !this.watchForm.name.trim()) {
      this.snackBar.open('Ticker and name are required.', 'Close', { duration: 3000 });
      return;
    }
    try {
      await this.watchlistApi.create({
        ticker: this.watchForm.ticker.trim().toUpperCase(),
        name: this.watchForm.name,
        accountType: this.watchForm.accountType,
        alertAbove: this.watchForm.alertAbove ?? undefined,
        alertBelow: this.watchForm.alertBelow ?? undefined,
      });
      this.watchForm.ticker = '';
      this.watchForm.name = '';
      this.snackBar.open('Added to watchlist.', 'Close', { duration: 3000 });
      await this.reloadAll();
    } catch {
      this.snackBar.open('Failed to add watchlist item.', 'Close', { duration: 3000 });
    }
  }

  async deleteWatch(id: string): Promise<void> {
    await this.watchlistApi.delete(id);
    await this.reloadAll();
  }

  private buildCharts(o: PortfolioOverview): void {
    const palette = ['#1976d2', '#2e7d32', '#f57c00', '#7b1fa2', '#00838f', '#c62828', '#5d4037', '#455a64'];
    this.allocationChart.set({
      labels: o.allocationByType.map((a) => a.label),
      datasets: [{
        data: o.allocationByType.map((a) => a.amountBrl),
        backgroundColor: o.allocationByType.map((_, i) => palette[i % palette.length]),
      }],
    });

    this.timelineChart.set({
      labels: o.timeline.map((t) => new Date(t.snapshotDate).toLocaleDateString()),
      datasets: [{
        label: 'Net worth (BRL)',
        data: o.timeline.map((t) => t.totalAmountBrl),
        borderColor: '#1976d2',
        backgroundColor: 'rgba(25,118,210,0.12)',
        fill: true,
        tension: 0.25,
      }],
    });

    this.benchmarkChart.set({
      labels: o.benchmarks.map((b) => b.code),
      datasets: [
        {
          label: 'Benchmark % a.a.',
          data: o.benchmarks.map((b) => b.annualRatePercent),
          backgroundColor: '#90a4ae',
        },
        {
          label: 'Portfolio estimated % a.a.',
          data: o.benchmarks.map((b) => b.portfolioEstimatedAnnualPercent),
          backgroundColor: '#1976d2',
        },
      ],
    });
  }
}
