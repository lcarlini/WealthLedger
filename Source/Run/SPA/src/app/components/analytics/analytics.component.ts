import { Component, inject, signal, OnInit, computed } from '@angular/core';
import { CurrencyPipe } from '@angular/common';

import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatTabsModule } from '@angular/material/tabs';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration } from 'chart.js';

import { Investment, Currency } from '../../models/investment';
import { InvestmentService } from '../../shared/services/investment.service';
import { MarketDataService } from '../../shared/services/market-data.service';
import { AnalyticsService, SpendingSummaryResponse } from '../../shared/services/analytics.service';
import { DashboardService } from '../../shared/services/dashboard.service';
import { AiInsightPayload, BrowserAiService, extractAiErrorMessage } from '../../core/services/browser-ai.service';

interface ProjectionRow {
  name: string;
  institutionName?: string;
  amount: number;
  currency: string;
  rateLabel: string;
  effectiveAnnualRatePercent: number;
  projectedIn12m: number;
  gainIn12m: number;
  belowInflation: boolean;
  optimizationHint?: string;
}

@Component({
  selector: 'app-analytics',
  standalone: true,
  imports: [
    CurrencyPipe,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatProgressBarModule,
    MatChipsModule,
    MatTooltipModule,
    MatTabsModule,
    MatButtonModule,
    MatSnackBarModule,
    BaseChartDirective,
  ],
  templateUrl: './analytics.component.html',
  styleUrl: './analytics.component.scss',
})
export class AnalyticsComponent implements OnInit {
  private readonly investmentService = inject(InvestmentService);
  private readonly marketDataService = inject(MarketDataService);
  private readonly analyticsService = inject(AnalyticsService);
  private readonly dashboardService = inject(DashboardService);
  private readonly browserAi = inject(BrowserAiService);
  private readonly snackBar = inject(MatSnackBar);

  private readonly monthNames = [
    'Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun',
    'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'
  ];

  // ── Investments ──
  loading = signal(true);
  investments = signal<Investment[]>([]);
  ipcaPerYear = signal<number | null>(null);
  selicPerYear = signal<number | null>(null);
  projections = computed(() => this.buildProjections(this.investments(), this.ipcaPerYear(), this.selicPerYear()));

  // ── Spending ──
  cardSpending = signal<SpendingSummaryResponse | null>(null);
  checkingSpending = signal<SpendingSummaryResponse | null>(null);
  loadingCard = signal(false);
  loadingChecking = signal(false);

  // ── AI Insights (WebLLM local) ──
  aiInsight = signal<string | null>(null);
  aiInsightLoading = signal(false);
  aiInsightError = signal<string | null>(null);
  aiModelReady = this.browserAi.ready;
  aiModelInitializing = this.browserAi.initializing;
  aiInitProgress = this.browserAi.initProgress;
  aiInitStatus = this.browserAi.initStatus;

  // ── Charts ──
  cardCategoryChartData = signal<ChartConfiguration<'bar'>['data']>({ labels: [], datasets: [] });
  checkingCategoryChartData = signal<ChartConfiguration<'bar'>['data']>({ labels: [], datasets: [] });

  barChartOptions: ChartConfiguration<'bar'>['options'] = {
    responsive: true,
    indexAxis: 'y',
    plugins: { legend: { display: false } },
    scales: {
      x: {
        beginAtZero: true,
        ticks: { color: 'rgba(128, 128, 128, 0.9)' },
        grid: { color: 'rgba(128, 128, 128, 0.12)' },
      },
      y: {
        ticks: { color: 'rgba(128, 128, 128, 0.9)' },
        grid: { color: 'rgba(128, 128, 128, 0.08)' },
      },
    },
  };

  ngOnInit(): void {
    this.load();
    this.loadCardSpending();
    this.loadCheckingSpending();
  }

  // ── Investment load ──
  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const [inv, md] = await Promise.all([
        this.investmentService.getAll(),
        this.marketDataService.getMarketData().catch(() => null),
      ]);
      this.investments.set(inv);
      this.ipcaPerYear.set(md?.ipcaPercentPerYear ?? null);
      this.selicPerYear.set(md?.selicPercentPerYear ?? null);
    } finally {
      this.loading.set(false);
    }
  }

  private buildProjections(investments: Investment[], ipca: number | null, selic: number | null): ProjectionRow[] {
    const rows: ProjectionRow[] = [];
    const cdiBasePercentPerYear = selic ?? 10;
    for (const i of investments) {
      const rateLabel = i.currency === Currency.USD || i.currency === Currency.EUR
        ? `${i.annualRatePercent}% a.a.`
        : `${i.cdiPercentage}% CDI`;
      const effectiveAnnualRatePercent = i.currency === Currency.BRL
        ? cdiBasePercentPerYear * (i.cdiPercentage / 100)
        : (i.annualRatePercent ?? 0);
      const projectedIn12m = i.amount * Math.pow(1 + effectiveAnnualRatePercent / 100, 1);
      const gainIn12m = projectedIn12m - i.amount;
      const belowInflation = ipca != null && effectiveAnnualRatePercent < ipca;
      let optimizationHint: string | undefined;
      if (belowInflation && ipca != null)
        optimizationHint = `Rate ${effectiveAnnualRatePercent.toFixed(2)}% is below IPCA (${ipca}%). Consider reallocating.`;
      else if (i.currency === Currency.BRL && i.cdiPercentage < 100)
        optimizationHint = 'Low CDI %. Compare with other options in the calculator.';

      rows.push({
        name: i.name,
        institutionName: i.institutionName,
        amount: i.amount,
        currency: i.currency === Currency.BRL ? 'BRL' : i.currency === Currency.USD ? 'USD' : 'EUR',
        rateLabel,
        effectiveAnnualRatePercent,
        projectedIn12m,
        gainIn12m,
        belowInflation: !!belowInflation,
        optimizationHint,
      });
    }
    return rows.sort((a, b) => b.gainIn12m - a.gainIn12m);
  }

  totalProjectedGainBrl = computed(() =>
    this.projections()
      .filter((p) => p.currency === 'BRL')
      .reduce((sum, p) => sum + p.gainIn12m, 0)
  );

  // ── Card Spending ──
  async loadCardSpending(): Promise<void> {
    this.loadingCard.set(true);
    try {
      const data = await this.analyticsService.getSpendingSummary('Card');
      this.cardSpending.set(data);
      this.cardCategoryChartData.set(this.buildCategoryChart(data));
    } catch {
      this.cardSpending.set(null);
    } finally {
      this.loadingCard.set(false);
    }
  }

  async loadCheckingSpending(): Promise<void> {
    this.loadingChecking.set(true);
    try {
      const data = await this.analyticsService.getSpendingSummary('Checking');
      this.checkingSpending.set(data);
      this.checkingCategoryChartData.set(this.buildCategoryChart(data));
    } catch {
      this.checkingSpending.set(null);
    } finally {
      this.loadingChecking.set(false);
    }
  }

  async recategorize(): Promise<void> {
    try {
      const res = await this.analyticsService.recategorize();
      this.snackBar.open(`Re-categorized ${res.updated} transactions.`, 'Close', { duration: 3000 });
      this.loadCardSpending();
      this.loadCheckingSpending();
    } catch {
      this.snackBar.open('Failed to recategorize.', 'Close', { duration: 3000 });
    }
  }

  async loadAiInsights(): Promise<void> {
    this.aiInsightError.set(null);
    this.aiInsight.set(null);
    this.aiInsightLoading.set(true);
    try {
      await this.browserAi.ensureReady();
      const payload = await this.buildAiPayload();
      const insight = await this.browserAi.generateInsight(payload);
      this.aiInsight.set(insight);
    } catch (err: unknown) {
      console.error('AI insight generation failed', err);
      this.aiInsightError.set(extractAiErrorMessage(err));
    } finally {
      this.aiInsightLoading.set(false);
    }
  }

  private async buildAiPayload(): Promise<AiInsightPayload> {
    const dashboard = await this.dashboardService.getDashboard().catch(() => null);
    const projections = this.projections();
    const card = this.cardSpending();
    const checking = this.checkingSpending();

    return {
      totalAmountBrl: dashboard?.totalAmount,
      investmentCount: projections.length,
      investments: projections.map((p) => ({
        name: p.name,
        amount: p.amount,
        currency: p.currency,
        rateLabel: p.rateLabel,
        gainIn12m: p.gainIn12m,
        belowInflation: p.belowInflation,
      })),
      cardCategories: (card?.byCategory ?? []).map((c) => ({
        category: c.category,
        totalAmount: c.totalAmount,
      })),
      checkingCategories: (checking?.byCategory ?? []).map((c) => ({
        category: c.category,
        totalAmount: c.totalAmount,
      })),
      projectedGainBrl: this.totalProjectedGainBrl(),
      ipca: this.ipcaPerYear(),
      selic: this.selicPerYear(),
    };
  }

  private buildCategoryChart(data: SpendingSummaryResponse): ChartConfiguration<'bar'>['data'] {
    const colors = [
      '#42A5F5', '#66BB6A', '#FFA726', '#AB47BC', '#EF5350',
      '#26C6DA', '#D4E157', '#FF7043', '#78909C', '#EC407A',
    ];
    return {
      labels: data.byCategory.map(c => c.category),
      datasets: [{
        label: 'Total Spent',
        data: data.byCategory.map(c => c.totalAmount),
        backgroundColor: data.byCategory.map((_, i) => colors[i % colors.length]),
      }],
    };
  }

  formatMonth(year: number, month: number): string {
    return `${this.monthNames[month - 1]} ${year}`;
  }

  getAllCategoriesForSpending(data: SpendingSummaryResponse): string[] {
    const cats = new Set<string>();
    data.byCategory.forEach(c => cats.add(c.category));
    return Array.from(cats);
  }

  getCategoryAmount(breakdown: { categories: { category: string; amount: number }[] }, category: string): number {
    return breakdown.categories.find(c => c.category === category)?.amount ?? 0;
  }

  getMonthTotal(breakdown: { categories: { category: string; amount: number }[] }): number {
    return breakdown.categories.reduce((sum, c) => sum + c.amount, 0);
  }
}
