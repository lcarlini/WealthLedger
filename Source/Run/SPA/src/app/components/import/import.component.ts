import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CurrencyPipe, DatePipe } from '@angular/common';

import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTooltipModule } from '@angular/material/tooltip';
import { firstValueFrom } from 'rxjs';

import { OfxImportService } from '../../shared/services/ofx-import.service';
import {
  StatementImportResponse,
  BankTransactionResponse,
} from '../../models/statement-import';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-import',
  standalone: true,
  imports: [
    FormsModule,
    CurrencyPipe,
    DatePipe,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatTableModule,
    MatSnackBarModule,
    MatProgressSpinnerModule,
    MatExpansionModule,
    MatDialogModule,
    MatTooltipModule,
  ],
  templateUrl: './import.component.html',
  styleUrl: './import.component.scss',
})
export class ImportComponent implements OnInit {
  private readonly ofxService = inject(OfxImportService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  imports = signal<StatementImportResponse[]>([]);
  loading = signal(false);
  uploading = signal(false);
  selectedFile = signal<File | null>(null);
  source = signal<'Card' | 'Checking'>('Checking');
  transactions = signal<BankTransactionResponse[]>([]);
  expandedImportId = signal<string | null>(null);

  /** Computed summary for the currently expanded import */
  summary = computed(() => {
    const txs = this.transactions();
    if (txs.length === 0) return null;
    const credits = txs.filter(t => t.amount > 0).reduce((s, t) => s + t.amount, 0);
    const debits = txs.filter(t => t.amount < 0).reduce((s, t) => s + t.amount, 0);
    return { credits, debits, balance: credits + debits, count: txs.length };
  });

  ngOnInit(): void {
    this.loadImports();
  }

  async loadImports(): Promise<void> {
    this.loading.set(true);
    try {
      this.imports.set(await this.ofxService.getImports());
    } finally {
      this.loading.set(false);
    }
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile.set(input.files?.[0] ?? null);
  }

  async upload(): Promise<void> {
    const file = this.selectedFile();
    if (!file) {
      this.snackBar.open('Select a file first.', 'Close', { duration: 3000 });
      return;
    }
    this.uploading.set(true);
    try {
      await this.ofxService.uploadOfx(file, this.source());
      this.snackBar.open('File imported successfully.', 'Close', { duration: 3000 });
      this.selectedFile.set(null);
      this.loadImports();
    } catch {
      this.snackBar.open('Import failed. Check the file format.', 'Close', { duration: 5000 });
    } finally {
      this.uploading.set(false);
    }
  }

  async onPanelToggle(importId: string, isOpen: boolean): Promise<void> {
    if (!isOpen) {
      this.expandedImportId.set(null);
      this.transactions.set([]);
      return;
    }
    this.expandedImportId.set(importId);
    this.transactions.set(await this.ofxService.getTransactions(importId));
  }

  async deleteImport(imp: StatementImportResponse, event: Event): Promise<void> {
    event.stopPropagation();
    const ref = this.dialog.open(ConfirmDialogComponent, {
      width: '400px',
      data: {
        title: 'Delete import',
        message: `Delete "${imp.fileName}" and all its ${imp.transactionCount} transactions?`,
      },
    });
    const ok = await firstValueFrom(ref.afterClosed());
    if (!ok) return;
    try {
      await this.ofxService.deleteImport(imp.id);
      this.snackBar.open('Import deleted.', 'Close', { duration: 3000 });
      if (this.expandedImportId() === imp.id) {
        this.expandedImportId.set(null);
        this.transactions.set([]);
      }
      this.loadImports();
    } catch {
      this.snackBar.open('Failed to delete import.', 'Close', { duration: 3000 });
    }
  }

  sourceLabel(source: number): string {
    return source === 1 ? 'Card' : 'Checking';
  }
}
