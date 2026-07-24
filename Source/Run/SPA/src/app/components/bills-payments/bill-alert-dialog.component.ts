import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';

import {
  BillCurrency,
  BillPaymentAlert,
  BillPaymentAlertRequest,
  BillRecurrence,
  BillRecurrenceLabels,
} from '../../models/bill-payment-alert';

export interface BillAlertDialogData {
  alert?: BillPaymentAlert;
}

@Component({
  selector: 'app-bill-alert-dialog',
  standalone: true,
  imports: [
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ isEdit ? 'Edit Alert' : 'New Bill Alert' }}</h2>
    <mat-dialog-content>
      <div class="form-grid">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Name</mat-label>
          <input matInput [(ngModel)]="form.name" name="name" required maxlength="200"
                 placeholder="e.g. Maple Bear School" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Amount</mat-label>
          <input matInput type="number" min="0" step="0.01" [(ngModel)]="form.amount" name="amount" required />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Currency</mat-label>
          <mat-select [(ngModel)]="form.currency" name="currency">
            <mat-option [value]="BillCurrency.BRL">BRL (R$)</mat-option>
            <mat-option [value]="BillCurrency.USD">USD ($)</mat-option>
            <mat-option [value]="BillCurrency.EUR">EUR (€)</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Due day</mat-label>
          <input matInput type="number" min="1" max="31" [(ngModel)]="form.dueDay" name="dueDay" required />
          <mat-hint>Day of each period (e.g. 10)</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Recurrence</mat-label>
          <mat-select [(ngModel)]="form.recurrence" name="recurrence">
            @for (opt of recurrenceOptions; track opt.value) {
              <mat-option [value]="opt.value">{{ opt.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Start month</mat-label>
          <mat-select [(ngModel)]="form.startMonth" name="startMonth">
            @for (m of months; track m) {
              <mat-option [value]="m">{{ monthNames[m - 1] }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Start year</mat-label>
          <input matInput type="number" min="2000" max="2100" [(ngModel)]="form.startYear" name="startYear" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>End month (optional)</mat-label>
          <mat-select [(ngModel)]="form.endMonth" name="endMonth">
            <mat-option [value]="null">None (ongoing)</mat-option>
            @for (m of months; track m) {
              <mat-option [value]="m">{{ monthNames[m - 1] }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>End year (optional)</mat-label>
          <input matInput type="number" min="2000" max="2100"
                 [(ngModel)]="form.endYear" name="endYear"
                 [disabled]="form.endMonth == null" />
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Notes</mat-label>
          <textarea matInput rows="2" [(ngModel)]="form.notes" name="notes" maxlength="500"></textarea>
        </mat-form-field>

        <mat-checkbox [(ngModel)]="form.isActive" name="isActive">Active</mat-checkbox>
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-stroked-button type="button" (click)="dialogRef.close()">Cancel</button>
      <button mat-raised-button color="primary" type="button" (click)="save()" [disabled]="!canSave()">
        {{ isEdit ? 'Save' : 'Create' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    mat-dialog-content { min-width: min(520px, 90vw); padding-top: 8px; }
    .form-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 4px 12px;
    }
    .full { grid-column: 1 / -1; }
  `],
})
export class BillAlertDialogComponent implements OnInit {
  readonly dialogRef = inject(MatDialogRef<BillAlertDialogComponent, BillPaymentAlertRequest | undefined>);
  readonly data = inject<BillAlertDialogData>(MAT_DIALOG_DATA);

  readonly BillCurrency = BillCurrency;
  readonly months = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
  readonly monthNames = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'];
  readonly recurrenceOptions = [
    { value: BillRecurrence.Monthly, label: BillRecurrenceLabels[BillRecurrence.Monthly] },
    { value: BillRecurrence.Yearly, label: BillRecurrenceLabels[BillRecurrence.Yearly] },
    { value: BillRecurrence.Once, label: BillRecurrenceLabels[BillRecurrence.Once] },
  ];

  isEdit = false;
  form: BillPaymentAlertRequest = {
    name: '',
    amount: 0,
    currency: BillCurrency.BRL,
    dueDay: 10,
    recurrence: BillRecurrence.Monthly,
    startYear: new Date().getFullYear(),
    startMonth: new Date().getMonth() + 1,
    endYear: null,
    endMonth: null,
    isActive: true,
    notes: '',
  };

  ngOnInit(): void {
    const alert = this.data.alert;
    if (!alert) return;

    this.isEdit = true;
    this.form = {
      id: alert.id,
      name: alert.name,
      notes: alert.notes ?? '',
      amount: alert.amount,
      currency: this.parseCurrency(alert.currency),
      dueDay: alert.dueDay,
      recurrence: this.parseRecurrence(alert.recurrence),
      startYear: alert.startYear,
      startMonth: alert.startMonth,
      endYear: alert.endYear ?? null,
      endMonth: alert.endMonth ?? null,
      isActive: alert.isActive,
    };
  }

  canSave(): boolean {
    return !!this.form.name.trim() && this.form.dueDay >= 1 && this.form.dueDay <= 31 && this.form.amount >= 0;
  }

  save(): void {
    if (!this.canSave()) return;

    const endMonth = this.form.endMonth ?? null;
    const endYear = endMonth == null ? null : (this.form.endYear ?? null);

    this.dialogRef.close({
      ...this.form,
      name: this.form.name.trim(),
      notes: this.form.notes?.trim() || undefined,
      endMonth,
      endYear: endMonth == null ? null : endYear,
    });
  }

  private parseCurrency(value: string): BillCurrency {
    switch (value) {
      case 'USD': return BillCurrency.USD;
      case 'EUR': return BillCurrency.EUR;
      default: return BillCurrency.BRL;
    }
  }

  private parseRecurrence(value: string): BillRecurrence {
    switch (value) {
      case 'Yearly': return BillRecurrence.Yearly;
      case 'Once': return BillRecurrence.Once;
      default: return BillRecurrence.Monthly;
    }
  }
}
