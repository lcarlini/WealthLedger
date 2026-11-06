import { Component, inject, signal, OnInit } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';

import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatTabsModule } from '@angular/material/tabs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

import {
  BillPaymentAlert,
  BillPaymentAlertRequest,
  BillPaymentOccurrence,
  BillRecurrence,
  BillRecurrenceLabels,
} from '../../models/bill-payment-alert';
import { BillPaymentAlertService } from '../../shared/services/bill-payment-alert.service';
import { ConfirmDialogComponent } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import { BillAlertDialogComponent } from './bill-alert-dialog.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';

@Component({
  selector: 'app-bills-payments',
  standalone: true,
  imports: [
    CurrencyPipe,
    DatePipe,
    FormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatSnackBarModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatTabsModule,
    MatFormFieldModule,
    MatInputModule,
    MatDialogModule,
    MatSlideToggleModule,
    PageHeaderComponent,
  ],
  templateUrl: './bills-payments.component.html',
  styleUrl: './bills-payments.component.scss',
})
export class BillsPaymentsComponent implements OnInit {
  private readonly service = inject(BillPaymentAlertService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  dueColumns = ['status', 'name', 'amount', 'dueDate', 'recurrence', 'actions'];
  upcomingColumns = ['status', 'name', 'amount', 'dueDate', 'recurrence'];
  pastColumns = ['name', 'amount', 'dueDate', 'recurrence', 'paidDate'];
  manageColumns = ['name', 'amount', 'dueDay', 'recurrence', 'period', 'active', 'actions'];

  dueThisMonth = signal<BillPaymentOccurrence[]>([]);
  upcoming = signal<BillPaymentOccurrence[]>([]);
  past = signal<BillPaymentOccurrence[]>([]);
  alerts = signal<BillPaymentAlert[]>([]);

  loadingDue = signal(false);
  loadingUpcoming = signal(false);
  loadingPast = signal(false);
  loadingAlerts = signal(false);
  upcomingMonths = signal(12);

  private readonly monthNames = [
    'Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun',
    'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez',
  ];

  ngOnInit(): void {
    this.reloadAll();
  }

  async reloadAll(): Promise<void> {
    await Promise.all([
      this.loadDueThisMonth(),
      this.loadUpcoming(),
      this.loadPast(),
      this.loadAlerts(),
    ]);
  }

  async loadDueThisMonth(): Promise<void> {
    this.loadingDue.set(true);
    try {
      this.dueThisMonth.set(await this.service.getDueThisMonth());
    } catch {
      this.snackBar.open('Failed to load due alerts.', 'Close', { duration: 3000 });
    } finally {
      this.loadingDue.set(false);
    }
  }

  async loadUpcoming(): Promise<void> {
    this.loadingUpcoming.set(true);
    try {
      const months = Math.max(1, Math.floor(this.upcomingMonths() || 1));
      this.upcomingMonths.set(months);
      this.upcoming.set(await this.service.getUpcoming(months));
    } catch {
      this.snackBar.open('Failed to load upcoming alerts.', 'Close', { duration: 3000 });
    } finally {
      this.loadingUpcoming.set(false);
    }
  }

  async loadPast(): Promise<void> {
    this.loadingPast.set(true);
    try {
      this.past.set(await this.service.getPast());
    } catch {
      this.snackBar.open('Failed to load past alerts.', 'Close', { duration: 3000 });
    } finally {
      this.loadingPast.set(false);
    }
  }

  async loadAlerts(): Promise<void> {
    this.loadingAlerts.set(true);
    try {
      this.alerts.set(await this.service.getAll());
    } catch {
      this.snackBar.open('Failed to load alerts.', 'Close', { duration: 3000 });
    } finally {
      this.loadingAlerts.set(false);
    }
  }

  formatDue(row: BillPaymentOccurrence): string {
    return `${row.dueDay} ${this.monthNames[row.dueMonth - 1]} ${row.dueYear}`;
  }

  formatDueDayLabel(day: number, recurrence?: string): string {
    const suffix =
      day % 10 === 1 && day !== 11 ? 'st' :
      day % 10 === 2 && day !== 12 ? 'nd' :
      day % 10 === 3 && day !== 13 ? 'rd' : 'th';
    if (recurrence === 'Yearly') return `${day}${suffix} every year`;
    if (recurrence === 'Once') return `${day}${suffix}`;
    return `${day}${suffix} of every month`;
  }

  formatRecurrence(value: string): string {
    switch (value) {
      case 'Monthly': return BillRecurrenceLabels[BillRecurrence.Monthly];
      case 'Yearly': return BillRecurrenceLabels[BillRecurrence.Yearly];
      case 'Once': return BillRecurrenceLabels[BillRecurrence.Once];
      default: return value;
    }
  }

  formatPeriod(alert: BillPaymentAlert): string {
    const start = `${this.monthNames[alert.startMonth - 1]} ${alert.startYear}`;
    if (alert.endYear && alert.endMonth) {
      return `${start} → ${this.monthNames[alert.endMonth - 1]} ${alert.endYear}`;
    }
    return `${start} → ongoing`;
  }

  isDueSoon(row: BillPaymentOccurrence): boolean {
    if (row.isOverdue) return false;
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const due = new Date(row.dueYear, row.dueMonth - 1, row.dueDay);
    const diffDays = Math.ceil((due.getTime() - today.getTime()) / (1000 * 60 * 60 * 24));
    return diffDays >= 0 && diffDays <= 7;
  }

  async openCreateDialog(): Promise<void> {
    const result = await firstValueFrom(
      this.dialog.open(BillAlertDialogComponent, {
        width: '560px',
        data: {},
      }).afterClosed()
    );
    if (!result) return;
    await this.saveAlert(result);
  }

  async openEditDialog(alert: BillPaymentAlert): Promise<void> {
    const result = await firstValueFrom(
      this.dialog.open(BillAlertDialogComponent, {
        width: '560px',
        data: { alert },
      }).afterClosed()
    );
    if (!result) return;
    await this.saveAlert(result, alert.id);
  }

  async editFromOccurrence(row: BillPaymentOccurrence): Promise<void> {
    try {
      const alert = await this.service.get(row.billPaymentAlertId);
      await this.openEditDialog(alert);
    } catch {
      this.snackBar.open('Failed to load alert.', 'Close', { duration: 3000 });
    }
  }

  private async saveAlert(body: BillPaymentAlertRequest, id?: string): Promise<void> {
    try {
      if (id) {
        await this.service.update(id, body);
        this.snackBar.open('Alert updated.', 'Close', { duration: 3000 });
      } else {
        await this.service.create(body);
        this.snackBar.open('Alert created.', 'Close', { duration: 3000 });
      }
      await this.reloadAll();
    } catch {
      this.snackBar.open('Failed to save alert.', 'Close', { duration: 3000 });
    }
  }

  async markPaid(row: BillPaymentOccurrence): Promise<void> {
    if (row.isVirtual || !row.id) return;
    try {
      await this.service.markPaid(row.id);
      this.snackBar.open('Marked as paid.', 'Close', { duration: 3000 });
      await Promise.all([this.loadDueThisMonth(), this.loadPast(), this.loadUpcoming()]);
    } catch {
      this.snackBar.open('Failed to mark as paid.', 'Close', { duration: 3000 });
    }
  }

  async toggleActive(alert: BillPaymentAlert, isActive: boolean): Promise<void> {
    try {
      const currency =
        alert.currency === 'USD' ? 2 : alert.currency === 'EUR' ? 3 : 1;
      const recurrence =
        alert.recurrence === 'Yearly' ? BillRecurrence.Yearly :
        alert.recurrence === 'Once' ? BillRecurrence.Once :
        BillRecurrence.Monthly;

      await this.service.update(alert.id, {
        name: alert.name,
        notes: alert.notes,
        amount: alert.amount,
        currency,
        dueDay: alert.dueDay,
        recurrence,
        startYear: alert.startYear,
        startMonth: alert.startMonth,
        endYear: alert.endYear ?? null,
        endMonth: alert.endMonth ?? null,
        isActive,
      });
      await this.reloadAll();
    } catch {
      this.snackBar.open('Failed to update alert.', 'Close', { duration: 3000 });
      await this.loadAlerts();
    }
  }

  async deleteAlert(alert: BillPaymentAlert): Promise<void> {
    const confirmed = await firstValueFrom(
      this.dialog.open(ConfirmDialogComponent, {
        data: {
          title: 'Delete alert',
          message: `Delete "${alert.name}" and its payment history?`,
        },
      }).afterClosed()
    );
    if (!confirmed) return;

    try {
      await this.service.delete(alert.id);
      this.snackBar.open('Alert deleted.', 'Close', { duration: 3000 });
      await this.reloadAll();
    } catch {
      this.snackBar.open('Failed to delete alert.', 'Close', { duration: 3000 });
    }
  }
}
