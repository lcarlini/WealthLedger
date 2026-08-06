import { Component, inject, signal, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { CurrencyPipe, DatePipe, NgClass } from '@angular/common';

import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatCardModule } from '@angular/material/card';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonToggleModule } from '@angular/material/button-toggle';

import { Investment, AccountType, Currency, CurrencyLabels, isVariableIncome, InvestmentStatus } from '../../../models/investment';
import { FinancialInstitution } from '../../../models/financial-institution';
import { InvestmentService } from '../../../shared/services/investment.service';
import { FinancialInstitutionService } from '../../../shared/services/financial-institution.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { CsvImportResultDialogComponent } from '../../../shared/components/csv-import-result-dialog/csv-import-result-dialog.component';
import { TranslatePipe } from '../../../shared/i18n/translate.pipe';
import { LanguageService } from '../../../shared/i18n/language.service';
import { TranslationKey } from '../../../shared/i18n/translations';

@Component({
  selector: 'app-investment-list',
  standalone: true,
  imports: [
    FormsModule, CurrencyPipe, DatePipe, NgClass,
    MatTableModule, MatButtonModule, MatIconModule, MatInputModule,
    MatFormFieldModule, MatPaginatorModule, MatCardModule, MatSnackBarModule,
    MatDialogModule, MatTooltipModule, MatProgressSpinnerModule,
    MatChipsModule, MatSelectModule, MatButtonToggleModule,
    TranslatePipe,
  ],
  templateUrl: './investment-list.component.html',
  styleUrl: './investment-list.component.scss',
})
export class InvestmentListComponent implements OnInit {
  private readonly service = inject(InvestmentService);
  private readonly institutionService = inject(FinancialInstitutionService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);
  private readonly lang = inject(LanguageService);

  readonly exportCsvUrl = this.service.getExportCsvUrl();
  readonly importCsvTemplateUrl = this.service.getImportCsvTemplateUrl();
  refreshingPrices = signal(false);

  displayedColumns = ['institutionName', 'name', 'accountType', 'currency', 'amount', 'principal', 'rate', 'ticker', 'maturityDate', 'actions'];
  allItems = signal<Investment[]>([]);
  dataSource = signal<Investment[]>([]);
  institutions = signal<FinancialInstitution[]>([]);
  totalCount = signal(0);
  pageSize = signal(50);
  pageIndex = signal(0);
  searchValue = '';
  loading = signal(false);
  statusFilter = signal<'active' | 'archived' | 'all'>('active');
  activeCount = signal(0);
  archivedCount = signal(0);

  ngOnInit(): void {
    const routeStatus = this.route.snapshot.data['status'] as string | undefined;
    const queryStatus = this.route.snapshot.queryParamMap.get('status');
    if (routeStatus === 'archived' || queryStatus === 'archived') {
      this.statusFilter.set('archived');
    } else if (queryStatus === 'all' || queryStatus === 'active') {
      this.statusFilter.set(queryStatus);
    }

    this.route.queryParamMap.subscribe(params => {
      if (this.route.snapshot.routeConfig?.path === 'investments/archived') {
        this.statusFilter.set('archived');
        this.pageIndex.set(0);
        this.applyFilters();
        return;
      }
      const status = params.get('status');
      if (status === 'archived' || status === 'all' || status === 'active') {
        this.statusFilter.set(status);
        this.pageIndex.set(0);
      }
      this.applyFilters();
    });
    this.loadInstitutions();
    this.loadData();
  }

  async loadInstitutions(): Promise<void> {
    try {
      this.institutions.set(await this.institutionService.getAll());
    } catch {
      this.institutions.set([]);
      this.snackBar.open('Failed to load institutions for filters.', 'Close', { duration: 4000 });
    }
  }

  async loadData(): Promise<void> {
    this.loading.set(true);
    try {
      const items = await this.service.getAll();
      this.allItems.set(items);
      this.activeCount.set(items.filter(i => (i.status ?? InvestmentStatus.Active) === InvestmentStatus.Active).length);
      this.archivedCount.set(items.filter(i => i.status === InvestmentStatus.Archived).length);
      this.applyFilters();
    } catch {
      this.snackBar.open('Failed to load investments.', 'Close', { duration: 3000 });
    } finally {
      this.loading.set(false);
    }
  }

  applyFilters(): void {
    const q = this.searchValue.trim().toLowerCase();
    let rows = this.allItems();
    const filter = this.statusFilter();
    if (filter === 'active') {
      rows = rows.filter(i => (i.status ?? InvestmentStatus.Active) === InvestmentStatus.Active);
    } else if (filter === 'archived') {
      rows = rows.filter(i => i.status === InvestmentStatus.Archived);
    }
    if (q) {
      rows = rows.filter(i =>
        i.name.toLowerCase().includes(q)
        || (i.institutionName ?? '').toLowerCase().includes(q)
        || (i.ticker ?? '').toLowerCase().includes(q));
    }

    this.totalCount.set(rows.length);
    const start = this.pageIndex() * this.pageSize();
    this.dataSource.set(rows.slice(start, start + this.pageSize()));
  }

  setStatusFilter(value: 'active' | 'archived' | 'all'): void {
    this.statusFilter.set(value);
    this.pageIndex.set(0);
    if (value === 'archived') {
      void this.router.navigate(['/investments', 'archived']);
    } else if (value === 'all') {
      void this.router.navigate(['/investments'], { queryParams: { status: 'all' } });
    } else {
      void this.router.navigate(['/investments']);
    }
    this.applyFilters();
  }

  needsHistorySetup(row: Investment): boolean {
    const principal = row.principalAmount ?? 0;
    return principal <= 0 || Math.abs(principal - row.amount) < 0.01;
  }

  onSearch(value: string): void {
    this.searchValue = value;
    this.pageIndex.set(0);
    this.applyFilters();
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.applyFilters();
  }

  navigateToCreate(): void {
    this.router.navigate(['/investments', 'new']);
  }

  navigateToDetail(id: string): void {
    this.router.navigate(['/investments', id]);
  }

  navigateToEdit(id: string): void {
    this.router.navigate(['/investments', id, 'edit']);
  }

  async refreshPrices(): Promise<void> {
    this.refreshingPrices.set(true);
    try {
      const result = await this.service.refreshPrices();
      const parts = [
        `${result.updated} updated`,
        result.failed ? `${result.failed} failed` : null,
        result.skipped ? `${result.skipped} skipped (missing ticker/qty)` : null,
      ].filter(Boolean);
      this.snackBar.open(`Prices: ${parts.join(', ')}.`, 'Close', { duration: 7000 });

      const failures = (result.items ?? []).filter(i => i.status === 'Failed');
      if (failures.length > 0) {
        const detail = failures
          .slice(0, 5)
          .map(f => `${f.ticker ?? f.name}: ${f.message ?? 'failed'}`)
          .join(' · ');
        this.snackBar.open(detail, 'Close', { duration: 9000 });
      }

      await this.loadData();
    } catch {
      this.snackBar.open('Failed to refresh prices.', 'Close', { duration: 4000 });
    } finally {
      this.refreshingPrices.set(false);
    }
  }

  getTypeLabel(type: AccountType): string {
    this.lang.lang();
    return this.lang.t(`account.${AccountType[type]}` as TranslationKey, AccountType[type]);
  }

  getCurrencyLabel(c: Currency): string {
    return CurrencyLabels[c] ?? 'BRL';
  }

  getCurrencyCode(c: Currency): string {
    return CurrencyLabels[c] ?? 'BRL';
  }

  getRateLabel(row: Investment): string {
    if (isVariableIncome(row.accountType)) {
      if (row.quantity && row.averagePrice != null && row.averagePrice > 0) {
        const cost = row.quantity * row.averagePrice;
        const gainPct = cost > 0 ? ((row.amount - row.averagePrice * row.quantity) / cost) * 100 : 0;
        return `Market · ${gainPct >= 0 ? '+' : ''}${gainPct.toFixed(1)}%`;
      }
      return 'Market';
    }
    if (row.currency === Currency.USD || row.currency === Currency.EUR) {
      return row.annualRatePercent != null ? `${row.annualRatePercent}% a.a.` : '—';
    }
    return `${row.cdiPercentage}% CDI`;
  }

  getTypeClass(type: AccountType): string {
    switch (type) {
      case AccountType.CheckingAccount: return 'type-checking';
      case AccountType.SavingsBox: return 'type-savings';
      case AccountType.FixedTerm: return 'type-fixed';
      case AccountType.Stock: return 'type-stock';
      case AccountType.FII: return 'type-fii';
      case AccountType.ETF: return 'type-etf';
      case AccountType.InvestmentFund: return 'type-fund';
      case AccountType.BDR: return 'type-bdr';
      case AccountType.Crypto: return 'type-crypto';
      default: return '';
    }
  }

  async confirmDelete(investment: Investment): Promise<void> {
    if (investment.status === InvestmentStatus.Archived) {
      const dialogRef = this.dialog.open(ConfirmDialogComponent, {
        width: '400px',
        data: {
          title: 'Excluir permanentemente',
          message: `Apagar "${investment.name}" de forma irreversível?`,
        },
      });
      const confirmed = await firstValueFrom(dialogRef.afterClosed());
      if (confirmed) {
        try {
          await this.service.permanentDelete(investment.id);
          this.snackBar.open('Excluído permanentemente.', 'Close', { duration: 3000 });
          this.loadData();
        } catch {
          this.snackBar.open('Failed to delete investment.', 'Close', { duration: 3000 });
        }
      }
      return;
    }

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Arquivar investimento',
        message: `Arquivar "${investment.name}"? O histórico fica em Arquivados (menu lateral).`,
      },
    });

    const confirmed = await firstValueFrom(dialogRef.afterClosed());
    if (confirmed) {
      try {
        await this.service.archive(investment.id, { redeemedAmount: investment.amount });
        this.snackBar.open('Arquivado. Abra Arquivados no menu para ver.', 'Close', { duration: 5000 });
        this.loadData();
      } catch {
        this.snackBar.open('Falha ao arquivar.', 'Close', { duration: 3000 });
      }
    }
  }

  onCsvSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    void this.runCsvImport(file);
  }

  private async runCsvImport(file: File): Promise<void> {
    this.loading.set(true);
    try {
      const result = await this.service.importCsv(file);
      this.snackBar.open(
        `CSV import: ${result.created} created, ${result.updated} updated, ${result.failed} failed, ${result.skipped} blank rows skipped.`,
        'Close',
        { duration: 7000 }
      );
      if ((result.rowErrors?.length ?? 0) > 0) {
        this.dialog.open(CsvImportResultDialogComponent, {
          width: '560px',
          maxHeight: '90vh',
          data: result,
        });
      }
      await this.loadInstitutions();
      await this.loadData();
    } catch {
      this.snackBar.open('CSV import failed.', 'Close', { duration: 4000 });
    } finally {
      this.loading.set(false);
    }
  }
}
