import { Component, inject, signal, OnInit } from '@angular/core';
import { Router } from '@angular/router';
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

import { Investment, AccountType, AccountTypeLabels, Currency, CurrencyLabels, isVariableIncome } from '../../../models/investment';
import { FinancialInstitution } from '../../../models/financial-institution';
import { InvestmentService } from '../../../shared/services/investment.service';
import { FinancialInstitutionService } from '../../../shared/services/financial-institution.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { CsvImportResultDialogComponent } from '../../../shared/components/csv-import-result-dialog/csv-import-result-dialog.component';

@Component({
  selector: 'app-investment-list',
  standalone: true,
  imports: [
    FormsModule, CurrencyPipe, DatePipe, NgClass,
    MatTableModule, MatButtonModule, MatIconModule, MatInputModule,
    MatFormFieldModule, MatPaginatorModule, MatCardModule, MatSnackBarModule,
    MatDialogModule, MatTooltipModule, MatProgressSpinnerModule,
    MatChipsModule, MatSelectModule,
  ],
  templateUrl: './investment-list.component.html',
  styleUrl: './investment-list.component.scss',
})
export class InvestmentListComponent implements OnInit {
  private readonly service = inject(InvestmentService);
  private readonly institutionService = inject(FinancialInstitutionService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  readonly exportCsvUrl = this.service.getExportCsvUrl();
  readonly importCsvTemplateUrl = this.service.getImportCsvTemplateUrl();
  refreshingPrices = signal(false);

  displayedColumns = ['institutionName', 'name', 'accountType', 'currency', 'amount', 'rate', 'ticker', 'maturityDate', 'actions'];
  dataSource = signal<Investment[]>([]);
  institutions = signal<FinancialInstitution[]>([]);
  totalCount = signal(0);
  pageSize = signal(50);
  pageIndex = signal(0);
  searchValue = '';
  loading = signal(false);

  readonly accountTypeLabels = AccountTypeLabels;

  ngOnInit(): void {
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
      const result = await this.service.getPaged(
        this.pageIndex() + 1, this.pageSize(), this.searchValue || undefined
      );
      this.dataSource.set(result.items);
      this.totalCount.set(result.totalCount);
    } catch {
      this.snackBar.open('Failed to load investments.', 'Close', { duration: 3000 });
    } finally {
      this.loading.set(false);
    }
  }

  onSearch(value: string): void {
    this.searchValue = value;
    this.pageIndex.set(0);
    this.loadData();
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
    this.loadData();
  }

  navigateToCreate(): void {
    this.router.navigate(['/investments', 'new']);
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
    return this.accountTypeLabels[type] ?? 'Unknown';
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
        const gainPct = cost > 0 ? ((row.amount - cost) / cost) * 100 : 0;
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
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Delete Investment',
        message: `Are you sure you want to delete "${investment.name}"?`,
      },
    });

    const confirmed = await firstValueFrom(dialogRef.afterClosed());
    if (confirmed) {
      try {
        await this.service.delete(investment.id);
        this.snackBar.open('Investment deleted.', 'Close', { duration: 3000 });
        this.loadData();
      } catch {
        this.snackBar.open('Failed to delete investment.', 'Close', { duration: 3000 });
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
