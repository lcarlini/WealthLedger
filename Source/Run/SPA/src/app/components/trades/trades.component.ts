import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import { TranslatePipe } from '../../shared/i18n/translate.pipe';
import { LanguageService } from '../../shared/i18n/language.service';
import { Investment, AccountType, Currency } from '../../models/investment';
import { CostMethodName, PositionSummary, TaxReport, TradePerformance, TradeResponse, TradeSide } from '../../models/trade';
import { InvestmentService } from '../../shared/services/investment.service';
import { TradeService } from '../../shared/services/trade.service';

/** Only variable-income holdings take trades; fixed income is managed on the investment itself. */
const VARIABLE_INCOME: AccountType[] = [
  AccountType.Stock,
  AccountType.FII,
  AccountType.ETF,
  AccountType.InvestmentFund,
  AccountType.BDR,
  AccountType.Crypto,
];

@Component({
  selector: 'app-trades',
  standalone: true,
  imports: [
    FormsModule,
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatIconModule,
    MatSnackBarModule,
    MatProgressSpinnerModule,
    PageHeaderComponent,
    TranslatePipe,
  ],
  templateUrl: './trades.component.html',
  styleUrl: './trades.component.scss',
})
export class TradesComponent implements OnInit {
  private readonly investments = inject(InvestmentService);
  private readonly trades = inject(TradeService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  readonly lang = inject(LanguageService);

  readonly TradeSide = TradeSide;
  readonly holdings = signal<Investment[]>([]);
  readonly selectedId = signal<string | null>(null);
  readonly method = signal<CostMethodName>('weighted');
  readonly tradeList = signal<TradeResponse[]>([]);
  readonly position = signal<PositionSummary | null>(null);
  readonly performance = signal<TradePerformance | null>(null);
  readonly taxYear = signal(new Date().getFullYear());
  readonly taxReport = signal<TaxReport | null>(null);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly selected = computed(() => this.holdings().find(h => h.id === this.selectedId()) ?? null);
  readonly currencyCode = computed(() => {
    const c = this.selected()?.currency;
    if (c === Currency.USD) return 'USD';
    if (c === Currency.EUR) return 'EUR';
    return 'BRL';
  });

  /** Form model. Quantity and price are shown or hidden depending on the trade type. */
  form = {
    side: TradeSide.Buy as TradeSide,
    tradeDate: new Date().toISOString().slice(0, 10),
    quantity: null as number | null,
    unitPrice: null as number | null,
    fees: 0,
    notes: '',
  };

  readonly years = Array.from({ length: 8 }, (_, i) => new Date().getFullYear() - i);

  async ngOnInit(): Promise<void> {
    try {
      await this.loadHoldings();
      if (this.holdings().length > 0) {
        await this.select(this.holdings()[0].id);
      }
    } catch (err) {
      this.error.set(this.messageOf(err));
    }
    await this.loadTaxReport();
  }

  isDividend(): boolean {
    return this.form.side === TradeSide.Dividend;
  }

  isBonus(): boolean {
    return this.form.side === TradeSide.Bonus;
  }

  async select(id: string): Promise<void> {
    this.selectedId.set(id);
    await this.refreshHolding();
  }

  async setMethod(method: CostMethodName): Promise<void> {
    this.method.set(method);
    await this.refreshHolding();
  }

  async loadTaxReport(): Promise<void> {
    try {
      this.taxReport.set(await this.trades.taxReport(this.taxYear()));
    } catch (err) {
      this.error.set(this.messageOf(err));
    }
  }

  setTaxYear(year: string | number): void {
    this.taxYear.set(Number(year));
    void this.loadTaxReport();
  }

  /** Mirrors the backend rules so most mistakes are caught before a request is sent. */
  validationMessage(): string | null {
    const f = this.form;
    if (!this.selectedId()) return this.lang.t('trades.err.noHolding');
    if (!f.tradeDate) return this.lang.t('trades.err.date');
    if (this.isDividend()) {
      return !f.unitPrice || f.unitPrice <= 0 ? this.lang.t('trades.err.dividendAmount') : null;
    }
    if (this.isBonus()) {
      return !f.quantity || f.quantity <= 0 ? this.lang.t('trades.err.quantity') : null;
    }
    if (!f.quantity || f.quantity <= 0) return this.lang.t('trades.err.quantity');
    if (f.unitPrice === null || f.unitPrice < 0) return this.lang.t('trades.err.price');
    if (f.fees < 0) return this.lang.t('trades.err.fees');
    return null;
  }

  async addTrade(): Promise<void> {
    const problem = this.validationMessage();
    if (problem) {
      this.snackBar.open(problem, undefined, { duration: 4000 });
      return;
    }

    const f = this.form;
    const dividend = this.isDividend();
    const bonus = this.isBonus();
    this.busy.set(true);
    try {
      await this.trades.add({
        investmentId: this.selectedId()!,
        side: f.side,
        tradeDate: f.tradeDate,
        quantity: dividend ? 0 : (f.quantity ?? 0),
        unitPrice: bonus ? 0 : (f.unitPrice ?? 0),
        fees: dividend || bonus ? 0 : f.fees,
        notes: f.notes.trim() || null,
      });
      this.form = { ...this.form, quantity: null, unitPrice: null, fees: 0, notes: '' };
      this.snackBar.open(this.lang.t('trades.saved'), undefined, { duration: 3000 });
      await this.afterChange();
    } catch (err) {
      this.snackBar.open(this.messageOf(err), undefined, { duration: 5000 });
    } finally {
      this.busy.set(false);
    }
  }

  async removeTrade(trade: TradeResponse): Promise<void> {
    const confirmed = await this.confirm({
      title: this.lang.t('trades.deleteTitle'),
      message: this.lang.t('trades.deleteMessage'),
      confirmLabel: this.lang.t('trades.delete'),
      cancelLabel: this.lang.t('trades.cancel'),
      destructive: true,
    });
    if (!confirmed) return;

    this.busy.set(true);
    try {
      await this.trades.remove(trade.id);
      this.snackBar.open(this.lang.t('trades.deleted'), undefined, { duration: 3000 });
      await this.afterChange();
    } catch (err) {
      this.snackBar.open(this.messageOf(err), undefined, { duration: 5000 });
    } finally {
      this.busy.set(false);
    }
  }

  sideLabel(side: TradeSide): string {
    switch (side) {
      case TradeSide.Buy: return this.lang.t('trades.side.buy');
      case TradeSide.Sell: return this.lang.t('trades.side.sell');
      case TradeSide.Dividend: return this.lang.t('trades.side.dividend');
      case TradeSide.Bonus: return this.lang.t('trades.side.bonus');
      default: return '';
    }
  }

  categoryLabel(category: string): string {
    const key = 'trades.category.' + category;
    const label = this.lang.t(key);
    return label === key ? category : label;
  }

  formatXirr(value: number | null): string {
    return value === null ? this.lang.t('trades.xirrNa') : value.toFixed(2) + '%';
  }

  private async loadHoldings(): Promise<void> {
    const all = await this.investments.getAll();
    this.holdings.set(all.filter(i => VARIABLE_INCOME.includes(i.accountType)));
  }

  private async refreshHolding(): Promise<void> {
    const id = this.selectedId();
    if (!id) return;
    this.loading.set(true);
    try {
      const [list, position, performance] = await Promise.all([
        this.trades.list(id),
        this.trades.position(id, this.method()),
        this.trades.performance(id, this.method()),
      ]);
      this.tradeList.set(list);
      this.position.set(position);
      this.performance.set(performance);
      this.error.set(null);
    } catch (err) {
      this.error.set(this.messageOf(err));
    } finally {
      this.loading.set(false);
    }
  }

  /** Quantities on the holding change with every trade, so the picker and the tax report are reloaded too. */
  private async afterChange(): Promise<void> {
    await Promise.all([this.refreshHolding(), this.loadTaxReport(), this.loadHoldings()]);
  }

  private async confirm(data: ConfirmDialogData): Promise<boolean> {
    const ref = this.dialog.open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
      data,
      width: '440px',
    });
    return (await firstValueFrom(ref.afterClosed())) === true;
  }

  private messageOf(err: unknown): string {
    return err instanceof Error ? err.message : this.lang.t('trades.err.unknown');
  }
}
