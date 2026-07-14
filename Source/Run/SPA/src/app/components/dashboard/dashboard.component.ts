import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration } from 'chart.js';

import { DashboardService } from '../../shared/services/dashboard.service';
import { MarketDataService } from '../../shared/services/market-data.service';
import { CashFlowScheduleService } from '../../shared/services/cashflow-schedule.service';
import { InvestmentService } from '../../shared/services/investment.service';
import { DashboardResponse } from '../../models/dashboard';
import { MarketDataResponse } from '../../models/market-data';
import { SimulationMatrixResponse } from '../../models/cashflow-schedule';
import { Investment, Currency } from '../../models/investment';

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
    CurrencyPipe,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
    BaseChartDirective,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  private readonly dashboardService = inject(DashboardService);
  private readonly marketDataService = inject(MarketDataService);
  private readonly cashFlowService = inject(CashFlowScheduleService);
  private readonly investmentService = inject(InvestmentService);

  loading = signal(true);
  data = signal<DashboardResponse | null>(null);
  marketData = signal<MarketDataResponse | null>(null);

  // Doughnut chart (investments by type)
  chartData = signal<ChartConfiguration<'doughnut'>['data']>({
    labels: [],
    datasets: [],
  });

  chartOptions: ChartConfiguration<'doughnut'>['options'] = {
    responsive: true,
    plugins: {
      legend: { position: 'bottom' },
    },
  };

  // Line charts
  patrimonyChartData = signal<ChartConfiguration<'line'>['data']>({ labels: [], datasets: [] });
  debtChartData = signal<ChartConfiguration<'bar'>['data']>({ labels: [], datasets: [] });
  realPatrimonyChartData = signal<ChartConfiguration<'line'>['data']>({ labels: [], datasets: [] });
  incomeExpenseChartData = signal<ChartConfiguration<'bar'>['data']>({ labels: [], datasets: [] });

  // Monthly gain per investment
  gainMonthLabels = signal<string[]>([]);
  investmentGainRows = signal<InvestmentGainRow[]>([]);
  gainTotalsPerMonth = signal<number[]>([]);

  lineChartOptions: ChartConfiguration<'line'>['options'] = {
    responsive: true,
    plugins: { legend: { position: 'bottom' } },
    scales: { y: { beginAtZero: false } },
  };

  barChartOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    plugins: { legend: { position: 'bottom' } },
    scales: { y: { beginAtZero: true } },
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
        this.dashboardService.getDashboard(),
        this.marketDataService.getMarketData().catch(() => null),
        this.investmentService.getAll().catch(() => [] as Investment[]),
      ]);
      this.data.set(result);
      this.marketData.set(rates ?? null);

      if (result.investmentsByType.length > 0) {
        this.chartData.set({
          labels: result.investmentsByType.map(i => i.accountType),
          datasets: [{
            data: result.investmentsByType.map(i => i.totalAmount),
            backgroundColor: ['#42A5F5', '#66BB6A', '#FFA726', '#AB47BC', '#EF5350'],
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

  private buildInvestmentGainTable(investments: Investment[], marketData?: MarketDataResponse | null): void {
    const now = new Date();
    const months = 12;
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
    for (const inv of investments) {
      const isBRL = inv.currency === Currency.BRL;
      const isUSD = inv.currency === Currency.USD;
      const isEUR = inv.currency === Currency.EUR;
      const annualRatePercent = isBRL
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
  }

  formatNum(value: number | null | undefined): string {
    if (value == null) return '—';
    return value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  totalGainAll = computed(() =>
    this.investmentGainRows().reduce((acc, row) => acc + row.totalGain, 0)
  );

  async loadSimulationCharts(startingBalance: number): Promise<void> {
    try {
      const now = new Date();
      const sim = await this.cashFlowService.getSimulation(
        now.getFullYear(), now.getMonth() + 1, 12, startingBalance
      );
      this.buildCharts(sim);
    } catch {
      // silently fail for charts
    }
  }

  private buildCharts(sim: SimulationMatrixResponse): void {
    const labels = sim.monthColumns.map(c => c.label);

    // Patrimony Growth (accumulated)
    this.patrimonyChartData.set({
      labels,
      datasets: [{
        label: 'Patrimony',
        data: sim.accumulatedPerMonth,
        fill: true,
        tension: 0.3,
        borderColor: '#1976d2',
        backgroundColor: 'rgba(25, 118, 210, 0.1)',
      }],
    });

    // Separate income and expenses per month
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

    // Debt chart
    this.debtChartData.set({
      labels,
      datasets: [{
        label: 'Monthly Debt/Expenses',
        data: debtPerMonth,
        backgroundColor: 'rgba(229, 57, 53, 0.7)',
      }],
    });

    // Real Patrimony (accumulated - cumulative debt)
    let cumDebt = 0;
    const realPatrimony = sim.accumulatedPerMonth.map((acc, i) => {
      cumDebt += debtPerMonth[i];
      return acc;
    });
    this.realPatrimonyChartData.set({
      labels,
      datasets: [
        {
          label: 'Patrimony',
          data: sim.accumulatedPerMonth,
          borderColor: '#1976d2',
          backgroundColor: 'rgba(25, 118, 210, 0.05)',
          fill: false,
          tension: 0.3,
        },
        {
          label: 'Real (after expenses)',
          data: realPatrimony,
          borderColor: '#2e7d32',
          backgroundColor: 'rgba(46, 125, 50, 0.05)',
          fill: false,
          tension: 0.3,
          borderDash: [5, 5],
        },
      ],
    });

    // Income vs Expenses
    this.incomeExpenseChartData.set({
      labels,
      datasets: [
        {
          label: 'Income',
          data: incomePerMonth,
          backgroundColor: 'rgba(46, 125, 50, 0.7)',
        },
        {
          label: 'Expenses',
          data: expensePerMonth,
          backgroundColor: 'rgba(229, 57, 53, 0.7)',
        },
      ],
    });
  }
}
