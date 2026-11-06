import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe, DecimalPipe, NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration } from 'chart.js';

import { DashboardService } from '../../shared/services/dashboard.service';
import { MarketDataService } from '../../shared/services/market-data.service';
import { CashFlowScheduleService } from '../../shared/services/cashflow-schedule.service';
import { InvestmentService } from '../../shared/services/investment.service';
import { DashboardResponse } from '../../models/dashboard';
import { MarketDataResponse } from '../../models/market-data';
import { SimulationMatrixResponse } from '../../models/cashflow-schedule';
import { Investment, Currency, hasDeterministicYield, InvestmentStatus } from '../../models/investment';
import { TranslatePipe } from '../../shared/i18n/translate.pipe';
import { LanguageService } from '../../shared/i18n/language.service';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';

interface InvestmentGainRow {
  name: string;
  institution?: string;
  amount: number;
  currency: string;
  annualRate: number;
  monthlyGains: number[];
  totalGain: number;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    RouterLink,
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    NgClass,
    FormsModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatTooltipModule,
    BaseChartDirective,
    TranslatePipe,
    PageHeaderComponent,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  private readonly dashboardService = inject(DashboardService);
  private readonly marketDataService = inject(MarketDataService);
  private readonly cashFlowService = inject(CashFlowScheduleService);
  private readonly investmentService = inject(InvestmentService);
  private readonly lang = inject(LanguageService);

  loading = signal(true);
  data = signal<DashboardResponse | null>(null);
  marketData = signal<MarketDataResponse | null>(null);
  projectionYears = signal(3);
  projectionEndDate = computed(() => {
    const today = new Date();
    return new Date(today.getFullYear() + this.projectionYears(), today.getMonth(), today.getDate());
  });
  private latestInvestments: Investment[] = [];
  private latestRates: MarketDataResponse | null = null;

  // Doughnut chart (investments by type)
  chartData = signal<ChartConfiguration<'doughnut'>['data']>({
    labels: [],
    datasets: [],
  });

  chartOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { position: 'bottom', labels: { boxWidth: 12 } },
    },
  };

  // Line / bar charts
  patrimonyChartData = signal<ChartConfiguration<'line'>['data']>({ labels: [], datasets: [] });
  debtChartData = signal<ChartConfiguration<'bar'>['data']>({ labels: [], datasets: [] });
  realPatrimonyChartData = signal<ChartConfiguration<'line'>['data']>({ labels: [], datasets: [] });
  incomeExpenseChartData = signal<ChartConfiguration<'bar'>['data']>({ labels: [], datasets: [] });

  // Fancy insight charts
  yieldPulseChartData = signal<ChartConfiguration<'line'>['data']>({ labels: [], datasets: [] });
  snowballChartData = signal<ChartConfiguration<'line'>['data']>({ labels: [], datasets: [] });
  topEarnersChartData = signal<ChartConfiguration<'bar'>['data']>({ labels: [], datasets: [] });
  byInstitutionChartData = signal<ChartConfiguration<'polarArea'>['data']>({ labels: [], datasets: [] });
  benchmarkRaceChartData = signal<ChartConfiguration<'line'>['data']>({ labels: [], datasets: [] });
  rateRadarChartData = signal<ChartConfiguration<'radar'>['data']>({ labels: [], datasets: [] });
  cashflowWaterfallChartData = signal<ChartConfiguration<'bar'>['data']>({ labels: [], datasets: [] });

  // Monthly gain per investment
  gainMonthLabels = signal<string[]>([]);
  investmentGainRows = signal<InvestmentGainRow[]>([]);
  gainTotalsPerMonth = signal<number[]>([]);
  hasFancyCharts = computed(() =>
    (this.yieldPulseChartData().labels?.length ?? 0) > 0
    || (this.byInstitutionChartData().labels?.length ?? 0) > 0
    || (this.realPatrimonyChartData().labels?.length ?? 0) > 0
  );

  private readonly chartPalette = [
    '#14b8a6', '#06b6d4', '#6366f1', '#8b5cf6', '#f59e0b',
    '#10b981', '#f43f5e', '#3b82f6', '#ec4899', '#84cc16',
  ];

  lineChartOptions: ChartConfiguration<'line'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { position: 'bottom', labels: { boxWidth: 12, usePointStyle: true } } },
    scales: {
      y: { beginAtZero: false, grid: { color: 'rgba(148,163,184,0.12)' } },
      x: { grid: { display: false }, ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 8 } },
    },
    elements: { point: { radius: 0, hoverRadius: 4 } },
  };

  fancyLineOptions: ChartConfiguration<'line'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: { position: 'bottom', labels: { boxWidth: 12, usePointStyle: true } },
      tooltip: { mode: 'index', intersect: false },
    },
    interaction: { mode: 'nearest', axis: 'x', intersect: false },
    scales: {
      y: { beginAtZero: false, grid: { color: 'rgba(148,163,184,0.12)' } },
      x: { grid: { display: false }, ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 8 } },
    },
    elements: { point: { radius: 0, hoverRadius: 4 } },
  };

  barChartOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { position: 'bottom' } },
    scales: {
      y: { beginAtZero: true, grid: { color: 'rgba(148,163,184,0.12)' } },
      x: { grid: { display: false }, ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 8 } },
    },
  };

  horizontalBarOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    indexAxis: 'y',
    plugins: { legend: { display: false } },
    scales: {
      x: { beginAtZero: true, grid: { color: 'rgba(148,163,184,0.12)' } },
      y: { grid: { display: false } },
    },
  };

  polarOptions: ChartConfiguration<'polarArea'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } } },
    scales: { r: { grid: { color: 'rgba(148,163,184,0.18)' }, ticks: { backdropColor: 'transparent' } } },
  };

  radarOptions: ChartConfiguration<'radar'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: {
      r: {
        beginAtZero: true,
        grid: { color: 'rgba(148,163,184,0.2)' },
        angleLines: { color: 'rgba(148,163,184,0.2)' },
        pointLabels: { font: { size: 11 } },
        ticks: { backdropColor: 'transparent', showLabelBackdrop: false },
      },
    },
  };

  waterfallOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { display: false } },
    scales: {
      y: { grid: { color: 'rgba(148,163,184,0.12)' } },
      x: { grid: { display: false }, ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 8 } },
    },
  };

  private readonly monthNames = [
    'Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun',
    'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'
  ];

  ngOnInit(): void {
    this.loadDashboard();
  }

  async loadDashboard(): Promise<void> {
    this.loading.set(true);
    try {
      const [result, rates, investments] = await Promise.all([
        this.dashboardService.getDashboard(this.projectionYears()),
        this.marketDataService.getMarketData().catch(() => null),
        this.investmentService.getAll().catch(() => [] as Investment[]),
      ]);
      this.data.set(result);
      this.marketData.set(rates ?? null);
      this.latestInvestments = investments;
      this.latestRates = rates ?? null;

      if (result.investmentsByType.length > 0) {
        this.chartData.set({
          labels: result.investmentsByType.map(i => i.accountType),
          datasets: [{
            data: result.investmentsByType.map(i => i.totalAmount),
            backgroundColor: [
              '#42A5F5', '#66BB6A', '#FFA726', '#AB47BC', '#EF5350',
              '#26A69A', '#7E57C2', '#8D6E63', '#5C6BC0',
            ],
          }],
        });
      }

      // Build monthly gain per investment (uses marketData for Selic and FX)
      this.buildInvestmentGainTable(investments, rates ?? undefined);

      // Load simulation for charts
      this.loadSimulationCharts(result.totalAmount);
    } finally {
      this.loading.set(false);
    }
  }

  setProjectionYears(value: number): void {
    const years = Math.max(1, Math.floor(Number(value) || 1));
    this.projectionYears.set(years);
    this.buildInvestmentGainTable(this.latestInvestments, this.latestRates);
    const totalAmount = this.data()?.totalAmount ?? 0;
    void this.loadProjectionData(totalAmount);
  }

  private async loadProjectionData(startingBalance: number): Promise<void> {
    const [dashboard] = await Promise.all([
      this.dashboardService.getDashboard(this.projectionYears()),
      this.loadSimulationCharts(startingBalance),
    ]);
    this.data.set(dashboard);
    this.buildFancyChartsFromGains();
  }

  private buildInvestmentGainTable(investments: Investment[], marketData?: MarketDataResponse | null): void {
    const now = new Date();
    const months = this.projectionYears() * 12;
    const labels: string[] = [];
    for (let i = 0; i < months; i++) {
      const d = new Date(now.getFullYear(), now.getMonth() + i, 1);
      labels.push(`${this.monthNames[d.getMonth()]} ${d.getFullYear()}`);
    }
    this.gainMonthLabels.set(labels);

    const selicPercentPerYear = marketData?.selicPercentPerYear ?? 10;
    const usdBrl = marketData?.usdBrl ?? 0;
    const eurBrl = marketData?.eurBrl ?? 0;

    const rows: InvestmentGainRow[] = [];
    for (const inv of investments.filter(i => (i.status ?? InvestmentStatus.Active) === InvestmentStatus.Active)) {
      const isBRL = inv.currency === Currency.BRL;
      const isUSD = inv.currency === Currency.USD;
      const isEUR = inv.currency === Currency.EUR;
      const annualRatePercent = !hasDeterministicYield(inv.accountType)
        ? 0
        : isBRL
          ? (selicPercentPerYear * (inv.cdiPercentage / 100))
          : (inv.annualRatePercent ?? 0);
      const monthlyRate = annualRatePercent === 0 ? 0 : Math.pow(1 + annualRatePercent / 100, 1 / 12) - 1;
      const monthlyGains: number[] = [];
      let balance = inv.amount;
      let totalGain = 0;
      const fx = isBRL ? 1 : isUSD ? usdBrl : eurBrl;
      for (let m = 0; m < months; m++) {
        const gainLocal = balance * monthlyRate;
        const gainBrl = fx > 0 ? gainLocal * fx : 0;
        monthlyGains.push(gainBrl);
        totalGain += gainBrl;
        balance += gainLocal;
      }
      rows.push({
        name: inv.name,
        institution: inv.institutionName,
        amount: inv.amount,
        currency: inv.currency === Currency.BRL ? 'BRL' : inv.currency === Currency.USD ? 'USD' : 'EUR',
        annualRate: annualRatePercent,
        monthlyGains,
        totalGain,
      });
    }
    this.investmentGainRows.set(rows.sort((a, b) => b.totalGain - a.totalGain));

    // Compute totals per month (all in BRL)
    const totals = new Array(months).fill(0);
    for (const row of rows) {
      for (let i = 0; i < months; i++) {
        totals[i] += row.monthlyGains[i];
      }
    }
    this.gainTotalsPerMonth.set(totals);
    this.buildFancyChartsFromGains();
  }

  formatNum(value: number | null | undefined): string {
    if (value == null) return '—';
    return value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  portfolioVerdictText(d: DashboardResponse): string {
    this.lang.lang();
    if (d.estimatedAnnualReturnPercent == null) return this.lang.t('dash.verdictEmpty');
    if ((d.excessOverIpcaPercent ?? 0) < 0) return this.lang.t('dash.verdictLagIpca');
    if ((d.excessOverCdiPercent ?? 0) >= 0.5) return this.lang.t('dash.verdictBeatCdi');
    if ((d.excessOverCdiPercent ?? 0) <= -0.5) return this.lang.t('dash.verdictLagCdi');
    return this.lang.t('dash.verdictMatch');
  }

  totalGainAll = computed(() =>
    this.investmentGainRows().reduce((acc, row) => acc + row.totalGain, 0)
  );

  async loadSimulationCharts(startingBalance: number): Promise<void> {
    try {
      const now = new Date();
      const sim = await this.cashFlowService.getSimulation(
        now.getFullYear(), now.getMonth() + 1, this.projectionYears() * 12, startingBalance
      );
      this.buildCharts(sim);
    } catch {
      // silently fail for charts
    }
  }

  private buildFancyChartsFromGains(): void {
    this.lang.lang();
    const labels = this.gainMonthLabels();
    const totals = this.gainTotalsPerMonth();
    const rows = this.investmentGainRows();
    const start = this.data()?.totalAmount ?? 0;
    const rates = this.latestRates;
    const dash = this.data();

    if (labels.length && totals.length) {
      this.yieldPulseChartData.set({
        labels,
        datasets: [{
          label: this.lang.t('dash.chartYieldPulseSeries'),
          data: totals,
          fill: true,
          tension: 0.45,
          borderColor: '#14b8a6',
          backgroundColor: 'rgba(20, 184, 166, 0.22)',
          borderWidth: 2.5,
        }],
      });

      let bal = start;
      const snowball = totals.map(g => {
        bal += g;
        return Math.round(bal * 100) / 100;
      });
      this.snowballChartData.set({
        labels,
        datasets: [{
          label: this.lang.t('dash.chartSnowballSeries'),
          data: snowball,
          fill: true,
          tension: 0.4,
          borderColor: '#6366f1',
          backgroundColor: 'rgba(99, 102, 241, 0.18)',
          borderWidth: 2.5,
        }],
      });

      const selic = rates?.selicPercentPerYear ?? dash?.selicPercent ?? 10;
      const ipca = rates?.ipcaPercentPerYear ?? dash?.ipcaPercent ?? 4.5;
      const portfolioAa = dash?.estimatedAnnualReturnPercent ?? selic;
      const racePortfolio: number[] = [];
      const raceCdi: number[] = [];
      const raceIpca: number[] = [];
      for (let m = 1; m <= labels.length; m++) {
        racePortfolio.push(Math.round(start * Math.pow(1 + portfolioAa / 100, m / 12) * 100) / 100);
        raceCdi.push(Math.round(start * Math.pow(1 + selic / 100, m / 12) * 100) / 100);
        raceIpca.push(Math.round(start * Math.pow(1 + ipca / 100, m / 12) * 100) / 100);
      }
      this.benchmarkRaceChartData.set({
        labels,
        datasets: [
          {
            label: this.lang.t('dash.chartRaceYou'),
            data: racePortfolio,
            borderColor: '#14b8a6',
            backgroundColor: 'transparent',
            borderWidth: 3,
            tension: 0.35,
            fill: false,
          },
          {
            label: this.lang.t('dash.chartRaceCdi'),
            data: raceCdi,
            borderColor: '#06b6d4',
            backgroundColor: 'transparent',
            borderWidth: 2,
            borderDash: [6, 4],
            tension: 0.35,
            fill: false,
          },
          {
            label: this.lang.t('dash.chartRaceIpca'),
            data: raceIpca,
            borderColor: '#f43f5e',
            backgroundColor: 'transparent',
            borderWidth: 2,
            borderDash: [2, 3],
            tension: 0.35,
            fill: false,
          },
        ],
      });
    } else {
      this.yieldPulseChartData.set({ labels: [], datasets: [] });
      this.snowballChartData.set({ labels: [], datasets: [] });
      this.benchmarkRaceChartData.set({ labels: [], datasets: [] });
    }

    const top = [...rows].sort((a, b) => b.totalGain - a.totalGain).slice(0, 8);
    if (top.length) {
      this.topEarnersChartData.set({
        labels: top.map(r => r.name),
        datasets: [{
          label: this.lang.t('dash.chartTopEarnersSeries'),
          data: top.map(r => Math.round(r.totalGain * 100) / 100),
          backgroundColor: top.map((_, i) => this.chartPalette[i % this.chartPalette.length]),
          borderRadius: 8,
          borderSkipped: false,
        }],
      });
    } else {
      this.topEarnersChartData.set({ labels: [], datasets: [] });
    }

    const byInst = new Map<string, number>();
    for (const inv of this.latestInvestments.filter(i => (i.status ?? InvestmentStatus.Active) === InvestmentStatus.Active)) {
      const key = inv.institutionName?.trim() || '—';
      byInst.set(key, (byInst.get(key) ?? 0) + inv.amount);
    }
    const instEntries = [...byInst.entries()].sort((a, b) => b[1] - a[1]).slice(0, 8);
    if (instEntries.length) {
      this.byInstitutionChartData.set({
        labels: instEntries.map(([name]) => name),
        datasets: [{
          data: instEntries.map(([, amount]) => amount),
          backgroundColor: instEntries.map((_, i) => this.withAlpha(this.chartPalette[i % this.chartPalette.length], 0.75)),
          borderWidth: 0,
        }],
      });
    } else {
      this.byInstitutionChartData.set({ labels: [], datasets: [] });
    }

    const radarRows = [...rows].filter(r => r.annualRate > 0).sort((a, b) => b.annualRate - a.annualRate).slice(0, 6);
    if (radarRows.length >= 3) {
      this.rateRadarChartData.set({
        labels: radarRows.map(r => r.name.length > 14 ? r.name.slice(0, 13) + '…' : r.name),
        datasets: [{
          label: this.lang.t('dash.chartRatesSeries'),
          data: radarRows.map(r => Math.round(r.annualRate * 100) / 100),
          backgroundColor: 'rgba(99, 102, 241, 0.25)',
          borderColor: '#6366f1',
          borderWidth: 2,
          pointBackgroundColor: '#14b8a6',
          pointBorderColor: '#fff',
          pointRadius: 4,
        }],
      });
    } else {
      this.rateRadarChartData.set({ labels: [], datasets: [] });
    }
  }

  private withAlpha(hex: string, alpha: number): string {
    const n = parseInt(hex.slice(1), 16);
    const r = (n >> 16) & 255;
    const g = (n >> 8) & 255;
    const b = n & 255;
    return `rgba(${r},${g},${b},${alpha})`;
  }

  private buildCharts(sim: SimulationMatrixResponse): void {
    this.lang.lang();
    const labels = sim.monthColumns.map(c => c.label);

    this.patrimonyChartData.set({
      labels,
      datasets: [{
        label: this.lang.t('dash.chartPatrimony'),
        data: sim.accumulatedPerMonth,
        fill: true,
        tension: 0.4,
        borderColor: '#0ea5e9',
        backgroundColor: 'rgba(14, 165, 233, 0.15)',
        borderWidth: 2.5,
      }],
    });

    const incomePerMonth = new Array(sim.monthColumns.length).fill(0);
    const expensePerMonth = new Array(sim.monthColumns.length).fill(0);
    const debtPerMonth = new Array(sim.monthColumns.length).fill(0);

    for (const row of sim.rows) {
      for (let i = 0; i < sim.monthColumns.length; i++) {
        const val = row.amountsByMonth[i] ?? 0;
        if (row.itemType === 'IncomeGross' || row.itemType === 'ExtraIncome') {
          incomePerMonth[i] += val;
        } else if (row.itemType === 'IncomeTax') {
          expensePerMonth[i] += Math.abs(val);
        } else if (val < 0) {
          expensePerMonth[i] += Math.abs(val);
          debtPerMonth[i] += Math.abs(val);
        }
      }
    }

    this.debtChartData.set({
      labels,
      datasets: [{
        label: this.lang.t('dash.chartDebt'),
        data: debtPerMonth,
        backgroundColor: 'rgba(244, 63, 94, 0.7)',
        borderRadius: 4,
      }],
    });

    let cumDebt = 0;
    const realPatrimony = sim.accumulatedPerMonth.map((acc, i) => {
      cumDebt += debtPerMonth[i];
      return Math.round((acc - cumDebt) * 100) / 100;
    });
    this.realPatrimonyChartData.set({
      labels,
      datasets: [
        {
          label: this.lang.t('dash.chartPatrimony'),
          data: sim.accumulatedPerMonth,
          borderColor: '#0ea5e9',
          backgroundColor: 'rgba(14, 165, 233, 0.08)',
          fill: true,
          tension: 0.4,
          borderWidth: 2.5,
        },
        {
          label: this.lang.t('dash.chartRealPatSeries'),
          data: realPatrimony,
          borderColor: '#10b981',
          backgroundColor: 'rgba(16, 185, 129, 0.08)',
          fill: true,
          tension: 0.4,
          borderWidth: 2.5,
          borderDash: [6, 4],
        },
      ],
    });

    this.incomeExpenseChartData.set({
      labels,
      datasets: [
        {
          label: this.lang.t('dash.chartIncome'),
          data: incomePerMonth,
          backgroundColor: 'rgba(16, 185, 129, 0.75)',
          borderRadius: 4,
        },
        {
          label: this.lang.t('dash.chartExpenses'),
          data: expensePerMonth,
          backgroundColor: 'rgba(244, 63, 94, 0.75)',
          borderRadius: 4,
        },
      ],
    });

    // Net cashflow "pulse bars" — income minus expenses as diverging bars
    const net = incomePerMonth.map((inc, i) => Math.round((inc - expensePerMonth[i]) * 100) / 100);
    this.cashflowWaterfallChartData.set({
      labels,
      datasets: [{
        label: this.lang.t('dash.chartCashflowSeries'),
        data: net,
        backgroundColor: net.map(v => v >= 0 ? 'rgba(20, 184, 166, 0.8)' : 'rgba(244, 63, 94, 0.8)'),
        borderRadius: 4,
      }],
    });
  }
}
