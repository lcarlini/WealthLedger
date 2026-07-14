import { Component, inject, signal, OnInit } from '@angular/core';
import { DecimalPipe, CurrencyPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTabsModule } from '@angular/material/tabs';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration } from 'chart.js';

import { MarketDataService } from '../../shared/services/market-data.service';
import { DashboardService } from '../../shared/services/dashboard.service';

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
    MatCardModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatButtonModule,
    MatSlideToggleModule,
    MatTabsModule,
    MatProgressSpinnerModule,
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
    months: 12,
    deductIr: false,
    irRate: 22.5,
    deductInflation: false,
    inflationPerYear: 4.5,
  };
  yieldResult = signal<{ gross: number; net: number; netReal: number } | null>(null);

  // ── Comparison ──
  comparisonForm = {
    amount: 10000,
    months: 12,
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
    years: 10,
  };
  patrimonyProjection = signal<{ year: number; value: number }[]>([]);
  patrimonyChartData = signal<ChartConfiguration<'line'>['data']>({ labels: [], datasets: [] });

  // ── Gain/Loss ──
  gainForm = {
    initialAmount: 10000,
    ratePerYear: 12,
    months: 24,
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

  ngOnInit(): void {
    this.marketDataService.getMarketData().then((md) => {
      this.marketData.set({
        selic: md.selicPercentPerYear,
        ipca: md.ipcaPercentPerYear,
        poupanca: md.poupancaPercentPerYear,
      });
      if (md.selicPercentPerYear != null) this.comparisonForm.poupancaRate = md.poupancaPercentPerYear ?? 8;
      if (md.ipcaPercentPerYear != null) this.yieldForm.inflationPerYear = md.ipcaPercentPerYear;
    }).catch(() => {});

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

  // ── Yield calculator ──
  calcYield(): void {
    const { initialAmount, ratePerYear, months, deductIr, irRate, deductInflation, inflationPerYear } = this.yieldForm;
    const rateDecimal = ratePerYear / 100;
    const monthsFraction = months / 12;
    const gross = initialAmount * Math.pow(1 + rateDecimal, monthsFraction);
    let net = gross;
    if (deductIr && months > 0) {
      const gain = gross - initialAmount;
      net = initialAmount + gain * (1 - irRate / 100);
    }
    let netReal = net;
    if (deductInflation && inflationPerYear > 0) {
      const inflationFactor = Math.pow(1 + inflationPerYear / 100, monthsFraction);
      netReal = net / inflationFactor;
    }
    this.yieldResult.set({ gross, net, netReal });
  }

  // ── Comparison calculator ──
  calcComparison(): void {
    const { amount, months, poupancaRate, cdiRate, fixedRate } = this.comparisonForm;
    const n = months / 12;
    const poupanca = amount * Math.pow(1 + poupancaRate / 100, n);
    const cdi = amount * Math.pow(1 + cdiRate / 100, n);
    const fixed = amount * Math.pow(1 + fixedRate / 100, n);
    this.comparisonResult.set({ poupanca, cdi, fixed });
  }

  // ── Patrimony projection ──
  calcPatrimony(): void {
    const { initialAmount, monthlyContribution, ratePerYear, years } = this.patrimonyForm;
    const monthlyRate = ratePerYear / 100 / 12;
    const points: { year: number; value: number }[] = [];
    let v = initialAmount;
    points.push({ year: 0, value: v });
    for (let y = 1; y <= years; y++) {
      for (let m = 0; m < 12; m++) {
        v = v * (1 + monthlyRate) + monthlyContribution;
      }
      points.push({ year: y, value: v });
    }
    this.patrimonyProjection.set(points);
    this.patrimonyChartData.set({
      labels: points.map((p) => p.year.toString()),
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
    const { initialAmount, ratePerYear, months } = this.gainForm;
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
