import { Component, computed, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { CsvImportResult } from '../../../models/csv-import-result';

@Component({
  selector: 'app-csv-import-result-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>CSV import details</h2>
    <mat-dialog-content class="dialog-body">
      <p class="summary">{{ summaryText() }}</p>
      @if (errors().length > 0) {
        <p class="hint">Issues (showing first {{ errors().length }} of {{ rowErrorCount() }}):</p>
        <ul class="error-list">
          @for (e of errors(); track e.lineNumber + '-' + e.message + '-' + $index) {
            <li>
              <span class="line">Line {{ e.lineNumber }}</span>
              @if (e.field) {
                <span class="field">{{ e.field }}:</span>
              }
              {{ e.message }}
            </li>
          }
        </ul>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      @if (rowErrorCount() > 0) {
        <button mat-stroked-button type="button" (click)="downloadErrors()">Download errors</button>
      }
      <button mat-raised-button color="primary" mat-dialog-close>Close</button>
    </mat-dialog-actions>
  `,
  styles: `
    .dialog-body {
      min-width: 280px;
      max-width: 560px;
      max-height: 60vh;
      overflow: auto;
    }
    .summary {
      margin-top: 0;
    }
    .hint {
      font-size: 0.875rem;
      opacity: 0.85;
    }
    .error-list {
      margin: 0;
      padding-left: 1.25rem;
    }
    .error-list li {
      margin-bottom: 0.35rem;
    }
    .line {
      font-weight: 500;
      margin-right: 0.35rem;
    }
    .field {
      font-family: monospace;
      font-size: 0.85em;
    }
  `,
})
export class CsvImportResultDialogComponent {
  readonly data = inject<CsvImportResult>(MAT_DIALOG_DATA);

  readonly rowErrorCount = computed(() => this.data.rowErrors?.length ?? 0);

  readonly errors = computed(() => (this.data.rowErrors ?? []).slice(0, 50));

  summaryText(): string {
    const r = this.data;
    return `Rows: ${r.rowsTotal}. Created: ${r.created}, updated: ${r.updated}, failed: ${r.failed}, blank rows skipped: ${r.skipped}.`;
  }

  downloadErrors(): void {
    const lines = (this.data.rowErrors ?? []).map(
      (e) => `Line ${e.lineNumber}\t${e.field ?? ''}\t${e.message}`.trimEnd()
    );
    const blob = new Blob([lines.join('\n')], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'csv-import-errors.txt';
    a.click();
    URL.revokeObjectURL(url);
  }
}
