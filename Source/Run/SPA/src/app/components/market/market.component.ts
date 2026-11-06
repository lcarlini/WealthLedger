import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LanguageService } from '../../shared/i18n/language.service';
import { TranslatePipe } from '../../shared/i18n/translate.pipe';
import {
  MarketIndex,
  MarketIndicesSnapshot,
  MarketToolsService,
  TreasurySnapshot,
  sparklinePoints,
} from '../../shared/services/market-tools.service';

const SPARK_WIDTH = 120;
const SPARK_HEIGHT = 36;

@Component({
  selector: 'app-market',
  standalone: true,
  imports: [
    DatePipe,
    FormsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    PageHeaderComponent,
    TranslatePipe,
  ],
  templateUrl: './market.component.html',
  styleUrl: './market.component.scss',
})
export class MarketComponent implements OnInit {
  private readonly api = inject(MarketToolsService);
  readonly lang = inject(LanguageService);

  readonly sparkWidth = SPARK_WIDTH;
  readonly sparkHeight = SPARK_HEIGHT;

  readonly indices = signal<MarketIndicesSnapshot | null>(null);
  readonly treasury = signal<TreasurySnapshot | null>(null);
  readonly indicesError = signal<string | null>(null);
  readonly treasuryError = signal<string | null>(null);
  readonly loading = signal(true);
  readonly typeFilter = signal('');

  readonly bondTypes = computed(() => {
    const bonds = this.treasury()?.bonds ?? [];
    return [...new Set(bonds.map(b => b.type))];
  });

  readonly visibleBonds = computed(() => {
    const bonds = this.treasury()?.bonds ?? [];
    const type = this.typeFilter();
    return type ? bonds.filter(b => b.type === type) : bonds;
  });

  ngOnInit(): void {
    void this.refresh();
  }

  async refresh(): Promise<void> {
    this.loading.set(true);
    // Independent sources: one failing must not hide the other.
    await Promise.all([
      this.api.indices().then(
        v => { this.indices.set(v); this.indicesError.set(null); },
        e => this.indicesError.set(this.messageOf(e)),
      ),
      this.api.treasury().then(
        v => { this.treasury.set(v); this.treasuryError.set(null); },
        e => this.treasuryError.set(this.messageOf(e)),
      ),
    ]);
    this.loading.set(false);
  }

  private get locale(): string {
    return this.lang.lang() === 'pt' ? 'pt-BR' : 'en-US';
  }

  number(value: number, digits = 2): string {
    return new Intl.NumberFormat(this.locale, { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value);
  }

  brl(value: number): string {
    return new Intl.NumberFormat(this.locale, { style: 'currency', currency: 'BRL' }).format(value);
  }

  /** Signed percentage with an explicit arrow, so direction never depends on color alone. */
  change(value: number): string {
    const arrow = value > 0 ? '▲' : value < 0 ? '▼' : '■';
    return `${arrow} ${value > 0 ? '+' : ''}${this.number(value)}%`;
  }

  direction(value: number): 'up' | 'down' | 'flat' {
    return value > 0 ? 'up' : value < 0 ? 'down' : 'flat';
  }

  spark(index: MarketIndex): string {
    return sparklinePoints(index.points, this.sparkWidth, this.sparkHeight);
  }

  private messageOf(err: unknown): string {
    return err instanceof Error ? err.message : String(err);
  }
}
