import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { DecimalPipe, CurrencyPipe, PercentPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTabsModule } from '@angular/material/tabs';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatChipsModule } from '@angular/material/chips';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration } from 'chart.js';

import { MarketDataService } from '../../shared/services/market-data.service';
import { DashboardService } from '../../shared/services/dashboard.service';
import {
  ComparisonGlobalSettings,
  ComparisonProductLabels,
  ComparisonProductType,
  ComparisonRunResult,
  ComparisonScenarioInput,
  createDefaultScenario,
  createStarterScenarios,
} from '../../shared/calculators/investment-comparison.models';
import { runComparison } from '../../shared/calculators/investment-comparison.engine';

interface GainRow {
  month: number;
  value: number;
  gain: number;
  cumulativeGain: number;
}

interface LoanRow {
  month: number;
  payment: number;
  interest: number;
  amortization: number;
  balance: number;
}

@Component({
  selector: 'app-calculator',
  standalone: true,
  imports: [
    FormsModule,
    DecimalPipe,
    CurrencyPipe,
    PercentPipe,
    DatePipe,
    MatCardModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatButtonModule,
    MatSlideToggleModule,
    MatTabsModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatChipsModule,
    MatTableModule,
    MatTooltipModule,
    BaseChartDirective,
  ],
  templateUrl: './calculator.component.html',
  styleUrl: './calculator.component.scss',
})
export class CalculatorComponent implements OnInit {
  private readonly marketDataService = inject(MarketDataService);
  private readonly dashboardService = inject(DashboardService);

  marketData = signal<{ selic?: number; ipca?: number; poupanca?: number } | null>(null);

  // ── Yield ──
  yieldForm = {
    initialAmount: 10000,
    ratePerYear: 12,
    years: 3,
    deductIr: false,
    irRate: 22.5,
    deductInflation: false,
    inflationPerYear: 4.5,
  };
  yieldResult = signal<{ gross: number; net: number; netReal: number } | null>(null);

  // ── Comparison ──
  comparisonForm = {
    amount: 10000,
    years: 3,
    poupancaRate: 8,
    cdiRate: 12,
    fixedRate: 11,
  };
  comparisonResult = signal<{ poupanca: number; cdi: number; fixed: number } | null>(null);

  // ── Patrimony projection ──
  patrimonyForm = {
    initialAmount: 100000,
    monthlyContribution: 2000,
    ratePerYear: 11,
    years: 3,
  };
  patrimonyProjection = signal<{ date: Date; label: string; value: number }[]>([]);
  patrimonyChartData = signal<ChartConfiguration<'line'>['data']>({ labels: [], datasets: [] });

  // ── Gain/Loss ──
  gainForm = {
    initialAmount: 10000,
    ratePerYear: 12,
    years: 3,
  };
  gainRows = signal<GainRow[]>([]);
  gainYearlySummary = signal<{ year: number; valueAtEnd: number; yearGain: number }[]>([]);

  // ── Loan Simulator (Tabela Price) ──
  loanForm = {
    principal: 50000,
    ratePerYear: 12,
    termMonths: 36,
  };
  loanResult = signal<{ monthlyPayment: number; totalPaid: number; totalInterest: number } | null>(null);
  loanRows = signal<LoanRow[]>([]);

  // ── Emergency Fund ──
  emergencyForm = {
    monthlyExpenses: 5000,
    targetMonths: 6,
  };
  emergencyResult = signal<{ targetAmount: number; currentInvestments: number; monthsCovered: number; gap: number } | null>(null);

  // ── Investment comparison ──
  readonly productTypes = Object.values(ComparisonProductType);
  readonly productLabels = ComparisonProductLabels;
  comparisonScenarios = signal<ComparisonScenarioInput[]>(createStarterScenarios());
  comparisonSettings = signal<ComparisonGlobalSettings>({
    years: 3,
    cdiBasePercent: 12,
    poupancaPercent: 8,
    inflationPercent: 4.5,
    deductTaxes: true,
    deductFees: true,
    adjustForInflation: true,
  });
  newScenarioProductType = ComparisonProductType.Cdb;
  readonly ComparisonProductType = ComparisonProductType;
  investmentComparisonResult = signal<ComparisonRunResult | null>(null);
  comparisonRankBy = signal<'net' | 'real'>('real');
  comparisonLineChart = signal<ChartConfiguration<'line'>['data']>({ labels: [], datasets: [] });
  comparisonBarChart = signal<ChartConfiguration<'bar'>['data']>({ labels: [], datasets: [] });

  rankedComparison = computed(() => {
    const r = this.investmentComparisonResult();
    if (!r) return [];
    return this.comparisonRankBy() === 'real' ? r.rankedByRealGain : r.rankedByNetGain;
  });
  comparisonEndDate = computed(() => {
    const today = new Date();
    const years = Math.max(1, Math.floor(this.comparisonSettings().years || 1));
    return new Date(today.getFullYear() + years, today.getMonth(), today.getDate());
  });

  ngOnInit(): void {
    this.marketDataService.getMarketData().then((md) => {
      this.marketData.set({
        selic: md.selicPercentPerYear,
        ipca: md.ipcaPercentPerYear,
        poupanca: md.poupancaPercentPerYear,
      });
      if (md.selicPercentPerYear != null) this.comparisonForm.poupancaRate = md.poupancaPercentPerYear ?? 8;
      if (md.ipcaPercentPerYear != null) this.yieldForm.inflationPerYear = md.ipcaPercentPerYear;

      this.comparisonSettings.update((s) => ({
        ...s,
        cdiBasePercent: md.selicPercentPerYear ?? s.cdiBasePercent,
        poupancaPercent: md.poupancaPercentPerYear ?? s.poupancaPercent,
        inflationPercent: md.ipcaPercentPerYear ?? s.inflationPercent,
      }));
      this.runInvestmentComparison();
    }).catch(() => {
      this.runInvestmentComparison();
    });

    this.dashboardService.getDashboard().then((d) => {
      this.emergencyResult.update((r) => r ? { ...r, currentInvestments: d.totalAmount ?? 0 } : null);
      this.calcEmergency(d.totalAmount ?? 0);
    }).catch(() => {});

    this.calcYield();
    this.calcComparison();
    this.calcPatrimony();
    this.calcGain();
    this.calcLoan();
    this.calcEmergency(0);
  }

  // ── Investment comparison ──
  addComparisonScenario(productType: ComparisonProductType = ComparisonProductType.Cdb): void {
    const index = this.comparisonScenarios().length + 1;
    this.comparisonScenarios.update((list) => [...list, createDefaultScenario(productType, index)]);
    this.runInvestmentComparison();
  }

  removeComparisonScenario(id: string): void {
    this.comparisonScenarios.update((list) => list.filter((s) => s.id !== id));
    this.runInvestmentComparison();
  }

  updateComparisonScenario(id: string, patch: Partial<ComparisonScenarioInput>): void {
    this.comparisonScenarios.update((list) =>
      list.map((s) => (s.id === id ? { ...s, ...patch } : s)),
    );
    this.runInvestmentComparison();
  }

  onComparisonSettingsChange(): void {
    this.runInvestmentComparison();
  }

  patchComparisonSettings(patch: Partial<ComparisonGlobalSettings>): void {
    this.comparisonSettings.update((s) => ({ ...s, ...patch }));
    this.runInvestmentComparison();
  }

  runInvestmentComparison(): void {
    const scenarios = this.comparisonScenarios();
    if (scenarios.length === 0) {
      this.investmentComparisonResult.set(null);
      this.comparisonLineChart.set({ labels: [], datasets: [] });
      this.comparisonBarChart.set({ labels: [], datasets: [] });
      return;
    }

    const result = runComparison(scenarios, this.comparisonSettings());
    this.investmentComparisonResult.set(result);
    this.buildComparisonCharts(result);
  }

  private buildComparisonCharts(result: ComparisonRunResult): void {
    const palette = ['#1976d2', '#2e7d32', '#f57c00', '#7b1fa2', '#00838f', '#c62828', '#5d4037', '#455a64'];
    const today = new Date();
    const yearLabels = Array.from({ length: Math.ceil(result.months / 12) }, (_, i) => {
      const date = new Date(today.getFullYear() + i + 1, today.getMonth(), today.getDate());
      return date.toLocaleDateString(undefined, { month: 'short', year: 'numeric' });
    });

    this.comparisonLineChart.set({
      labels: yearLabels,
      datasets: result.scenarios.map((s, i) => ({
        label: s.name,
        data: yearLabels.map((_, yi) => {
          const monthIndex = Math.min((yi + 1) * 12, result.months) - 1;
          const pt = s.monthlyPoints[monthIndex];
          return pt?.balance ?? 0;
        }),
        borderColor: palette[i % palette.length],
        backgroundColor: palette[i % palette.length] + '33',
        tension: 0.25,
        fill: false,
      })),
    });

    const ranked = this.comparisonRankBy() === 'real' ? result.rankedByRealGain : result.rankedByNetGain;
    this.comparisonBarChart.set({
      labels: ranked.map((s) => s.name),
      datasets: [
        {
          label: 'Net final value',
          data: ranked.map((s) => s.netFinal),
          backgroundColor: ranked.map((_, i) => palette[i % palette.length] + 'cc'),
        },
        {
          label: 'Real net final',
          data: ranked.map((s) => s.realNetFinal),
          backgroundColor: ranked.map((_, i) => palette[i % palette.length] + '66'),
        },
      ],
    });
  }

  get comparisonLineOptions(): ChartConfiguration<'line'>['options'] {
    return {
      responsive: true,
      interaction: { mode: 'index', intersect: false },
      scales: { y: { beginAtZero: false } },
      plugins: { legend: { position: 'bottom' } },
    };
  }

  get comparisonBarOptions(): ChartConfiguration<'bar'>['options'] {
    return {
      responsive: true,
      scales: { y: { beginAtZero: true } },
      plugins: { legend: { position: 'bottom' } },
    };
  }

  isCdiRateMode(scenario: ComparisonScenarioInput): boolean {
    return scenario.rateMode === 'cdi';
  }

  isFixedRateMode(scenario: ComparisonScenarioInput): boolean {
    return scenario.rateMode === 'fixed';
  }

  isPoupancaRateMode(scenario: ComparisonScenarioInput): boolean {
    return scenario.rateMode === 'poupanca';
  }

  onProductTypeChange(scenario: ComparisonScenarioInput, productType: ComparisonProductType): void {
    const defaults = createDefaultScenario(productType, 1);
    this.updateComparisonScenario(scenario.id, {
      productType,
      name: defaults.name,
      rateMode: defaults.rateMode,
      annualRatePercent: defaults.annualRatePercent,
      cdiPercent: defaults.cdiPercent,
      adminFeePercentPerYear: defaults.adminFeePercentPerYear,
      custodyFeePercentPerYear: defaults.custodyFeePercentPerYear,
    });
  }

  // ── Yield calculator ──
  calcYield(): void {
    const { initialAmount, ratePerYear, years, deductIr, irRate, deductInflation, inflationPerYear } = this.yieldForm;
    const rateDecimal = ratePerYear / 100;
    const durationYears = Math.max(0, years);
    const gross = initialAmount * Math.pow(1 + rateDecimal, durationYears);
    let net = gross;
    if (deductIr && durationYears > 0) {
      const gain = gross - initialAmount;
      net = initialAmount + gain * (1 - irRate / 100);
    }
    let netReal = net;
    if (deductInflation && inflationPerYear > 0) {
      const inflationFactor = Math.pow(1 + inflationPerYear / 100, durationYears);
      netReal = net / inflationFactor;
    }
    this.yieldResult.set({ gross, net, netReal });
  }

  // ── Comparison calculator ──
  calcComparison(): void {
    const { amount, years, poupancaRate, cdiRate, fixedRate } = this.comparisonForm;
    const durationYears = Math.max(0, years);
    const poupanca = amount * Math.pow(1 + poupancaRate / 100, durationYears);
    const cdi = amount * Math.pow(1 + cdiRate / 100, durationYears);
    const fixed = amount * Math.pow(1 + fixedRate / 100, durationYears);
    this.comparisonResult.set({ poupanca, cdi, fixed });
  }

  // ── Patrimony projection ──
  calcPatrimony(): void {
    const { initialAmount, monthlyContribution, ratePerYear, years } = this.patrimonyForm;
    const monthlyRate = ratePerYear / 100 / 12;
    const today = new Date();
    const durationYears = Math.max(1, Math.floor(years));
    const points: { date: Date; label: string; value: number }[] = [];
    let v = initialAmount;
    points.push({ date: today, label: today.toLocaleDateString(undefined, { month: 'short', year: 'numeric' }), value: v });
    for (let y = 1; y <= durationYears; y++) {
      for (let m = 0; m < 12; m++) {
        v = v * (1 + monthlyRate) + monthlyContribution;
      }
      const date = new Date(today.getFullYear() + y, today.getMonth(), today.getDate());
      points.push({ date, label: date.toLocaleDateString(undefined, { month: 'short', year: 'numeric' }), value: v });
    }
    this.patrimonyProjection.set(points);
    this.patrimonyChartData.set({
      labels: points.map((p) => p.label),
      datasets: [{
        label: 'Projected patrimony',
        data: points.map((p) => p.value),
        fill: true,
        tension: 0.3,
        borderColor: '#1976d2',
        backgroundColor: 'rgba(25, 118, 210, 0.1)',
      }],
    });
  }

  get patrimonyChartOptions(): ChartConfiguration<'line'>['options'] {
    return {
      responsive: true,
      scales: {
        y: { beginAtZero: false },
      },
    };
  }

  // ── Gain/Loss calculator ──
  calcGain(): void {
    const { initialAmount, ratePerYear, years } = this.gainForm;
    const months = Math.max(1, Math.round(years * 12));
    const monthlyRate = Math.pow(1 + ratePerYear / 100, 1 / 12) - 1;
    const rows: GainRow[] = [];
    let value = initialAmount;
    let cumulativeGain = 0;
    for (let m = 1; m <= months; m++) {
      const prevValue = value;
      value = value * (1 + monthlyRate);
      const gain = value - prevValue;
      cumulativeGain += gain;
      rows.push({ month: m, value, gain, cumulativeGain });
    }
    this.gainRows.set(rows);

    // Yearly summary
    const yearly: { year: number; valueAtEnd: number; yearGain: number }[] = [];
    let yearStart = initialAmount;
    for (let y = 1; y <= Math.ceil(months / 12); y++) {
      const endMonth = Math.min(y * 12, months);
      const valueAtEnd = rows[endMonth - 1]?.value ?? yearStart;
      const yearGain = valueAtEnd - yearStart;
      yearly.push({ year: y, valueAtEnd, yearGain });
      yearStart = valueAtEnd;
    }
    this.gainYearlySummary.set(yearly);
  }

  // ── Loan Simulator (Tabela Price) ──
  calcLoan(): void {
    const { principal, ratePerYear, termMonths } = this.loanForm;
    if (principal <= 0 || ratePerYear <= 0 || termMonths <= 0) {
      this.loanResult.set(null);
      this.loanRows.set([]);
      return;
    }
    const monthlyRate = ratePerYear / 100 / 12;
    const payment = principal * (monthlyRate * Math.pow(1 + monthlyRate, termMonths)) /
      (Math.pow(1 + monthlyRate, termMonths) - 1);
    const totalPaid = payment * termMonths;
    const totalInterest = totalPaid - principal;
    this.loanResult.set({ monthlyPayment: payment, totalPaid, totalInterest });

    const rows: LoanRow[] = [];
    let balance = principal;
    for (let m = 1; m <= termMonths; m++) {
      const interest = balance * monthlyRate;
      const amortization = payment - interest;
      balance -= amortization;
      rows.push({
        month: m,
        payment,
        interest,
        amortization,
        balance: Math.max(balance, 0),
      });
    }
    this.loanRows.set(rows);
  }

  // ── Emergency Fund calculator ──
  private _currentInvestments = 0;

  calcEmergency(currentInvestments?: number): void {
    if (currentInvestments !== undefined) this._currentInvestments = currentInvestments;
    const { monthlyExpenses, targetMonths } = this.emergencyForm;
    const targetAmount = monthlyExpenses * targetMonths;
    const monthsCovered = monthlyExpenses > 0 ? this._currentInvestments / monthlyExpenses : 0;
    const gap = Math.max(targetAmount - this._currentInvestments, 0);
    this.emergencyResult.set({
      targetAmount,
      currentInvestments: this._currentInvestments,
      monthsCovered,
      gap,
    });
  }
}
