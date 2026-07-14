import { Component, inject, signal, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatCardModule } from '@angular/material/card';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DatePipe } from '@angular/common';

import { FinancialInstitution } from '../../../models/financial-institution';
import { FinancialInstitutionService } from '../../../shared/services/financial-institution.service';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { CsvImportResultDialogComponent } from '../../../shared/components/csv-import-result-dialog/csv-import-result-dialog.component';

@Component({
  selector: 'app-financial-institution-list',
  standalone: true,
  imports: [
    FormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatInputModule,
    MatFormFieldModule,
    MatPaginatorModule,
    MatToolbarModule,
    MatCardModule,
    MatSnackBarModule,
    MatDialogModule,
    MatTooltipModule,
    MatProgressSpinnerModule,
    DatePipe,
  ],
  templateUrl: './financial-institution-list.component.html',
  styleUrl: './financial-institution-list.component.scss',
})
export class FinancialInstitutionListComponent implements OnInit {
  private readonly service = inject(FinancialInstitutionService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  readonly exportCsvUrl = this.service.getExportCsvUrl();
  readonly importCsvTemplateUrl = this.service.getImportCsvTemplateUrl();

  displayedColumns = ['imageUrl', 'name', 'description', 'createdDate', 'actions'];
  dataSource = signal<FinancialInstitution[]>([]);
  totalCount = signal(0);
  pageSize = signal(50);
  pageIndex = signal(0);
  searchValue = '';
  loading = signal(false);

  ngOnInit(): void {
    this.loadData();
  }

  async loadData(): Promise<void> {
    this.loading.set(true);
    try {
      const result = await this.service.getPaged(
        this.pageIndex() + 1,
        this.pageSize(),
        this.searchValue || undefined
      );
      this.dataSource.set(result.items);
      this.totalCount.set(result.totalCount);
    } catch {
      this.snackBar.open('Failed to load financial institutions.', 'Close', { duration: 3000 });
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
    this.router.navigate(['/financial-institutions', 'new']);
  }

  navigateToEdit(id: string): void {
    this.router.navigate(['/financial-institutions', id, 'edit']);
  }

  async confirmDelete(institution: FinancialInstitution): Promise<void> {
    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Delete Financial Institution',
        message: `Are you sure you want to delete "${institution.name}"?`,
      },
    });

    const confirmed = await firstValueFrom(dialogRef.afterClosed());
    if (confirmed) {
      try {
        await this.service.delete(institution.id);
        this.snackBar.open('Financial institution deleted.', 'Close', { duration: 3000 });
        this.loadData();
      } catch {
        this.snackBar.open('Failed to delete financial institution.', 'Close', { duration: 3000 });
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
      await this.loadData();
    } catch {
      this.snackBar.open('CSV import failed.', 'Close', { duration: 4000 });
    } finally {
      this.loading.set(false);
    }
  }
}
