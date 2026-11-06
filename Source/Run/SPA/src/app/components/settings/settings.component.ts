import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { AppearancePanelComponent } from '../../shared/components/appearance-panel/appearance-panel.component';
import { TextSizePanelComponent } from '../../shared/components/text-size-panel/text-size-panel.component';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../shared/components/confirm-dialog/confirm-dialog.component';
import { LanguageService } from '../../shared/i18n/language.service';
import { AppLanguage } from '../../shared/i18n/translations';
import { TranslatePipe } from '../../shared/i18n/translate.pipe';
import {
  BACKUP_LIMITS,
  BackupInfo,
  BackupService,
  BackupStatus,
} from '../../shared/services/backup.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    FormsModule,
    DatePipe,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule,
    MatTooltipModule,
    PageHeaderComponent,
    AppearancePanelComponent,
    TextSizePanelComponent,
    TranslatePipe,
  ],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss',
})
export class SettingsComponent implements OnInit {
  readonly backups = inject(BackupService);
  readonly lang = inject(LanguageService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly limits = BACKUP_LIMITS;
  readonly status = signal<BackupStatus | null>(null);
  readonly list = signal<BackupInfo[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  // Form model for the backup policy (bound with ngModel).
  enabled = true;
  intervalDays = 15;
  maxBackups = 10;

  private static readonly reasonKeys: Record<string, string> = {
    manual: 'settings.backup.reason.manual',
    scheduled: 'settings.backup.reason.scheduled',
    'pre-upgrade': 'settings.backup.reason.preUpgrade',
    'pre-restore': 'settings.backup.reason.preRestore',
    'pre-import': 'settings.backup.reason.preImport',
  };

  ngOnInit(): void {
    void this.refresh();
  }

  setLang(lang: AppLanguage): void {
    this.lang.setLanguage(lang);
  }

  reasonKey(reason: string): string {
    return SettingsComponent.reasonKeys[reason] ?? 'settings.backup.reason.manual';
  }

  isValid(): boolean {
    return Number.isInteger(this.intervalDays)
      && this.intervalDays >= this.limits.intervalDaysMin
      && this.intervalDays <= this.limits.intervalDaysMax
      && Number.isInteger(this.maxBackups)
      && this.maxBackups >= this.limits.maxBackupsMin
      && this.maxBackups <= this.limits.maxBackupsMax;
  }

  formatBytes(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  async refresh(): Promise<void> {
    this.loading.set(true);
    try {
      const [status, list] = await Promise.all([this.backups.getStatus(), this.backups.list()]);
      this.status.set(status);
      this.list.set(list);
      this.enabled = status.settings.enabled;
      this.intervalDays = status.settings.intervalDays;
      this.maxBackups = status.settings.maxBackups;
      this.error.set(null);
    } catch (err) {
      this.error.set(this.messageOf(err));
    } finally {
      this.loading.set(false);
    }
  }

  async saveSettings(): Promise<void> {
    if (!this.isValid()) {
      this.toast(this.lang.t('settings.backup.invalid'));
      return;
    }
    await this.run(async () => {
      await this.backups.updateSettings({
        enabled: this.enabled,
        intervalDays: this.intervalDays,
        maxBackups: this.maxBackups,
      });
      await this.refresh();
      this.toast(this.lang.t('settings.backup.saved'));
    });
  }

  async backupNow(): Promise<void> {
    await this.run(async () => {
      await this.backups.createBackup();
      await this.refresh();
      this.toast(this.lang.t('settings.backup.createdToast'));
    });
  }

  async restore(item: BackupInfo): Promise<void> {
    const confirmed = await this.confirm({
      title: this.lang.t('settings.backup.restoreTitle'),
      message: `${this.lang.t('settings.backup.restoreMessage')} ${item.fileName}`,
      confirmLabel: this.lang.t('settings.backup.restore'),
      cancelLabel: this.lang.t('settings.cancel'),
      destructive: true,
    });
    if (!confirmed) return;

    await this.run(async () => {
      this.status.set(await this.backups.restore(item.fileName));
      this.toast(this.lang.t('settings.backup.restoreStaged'));
    });
  }

  async remove(item: BackupInfo): Promise<void> {
    const confirmed = await this.confirm({
      title: this.lang.t('settings.backup.deleteTitle'),
      message: `${this.lang.t('settings.backup.deleteMessage')} ${item.fileName}`,
      confirmLabel: this.lang.t('settings.backup.delete'),
      cancelLabel: this.lang.t('settings.cancel'),
      destructive: true,
    });
    if (!confirmed) return;

    await this.run(async () => {
      await this.backups.remove(item.fileName);
      await this.refresh();
      this.toast(this.lang.t('settings.backup.deleted'));
    });
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch (err) {
      this.toast(this.messageOf(err));
    } finally {
      this.busy.set(false);
    }
  }

  private async confirm(data: ConfirmDialogData): Promise<boolean> {
    const ref = this.dialog.open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
      data,
      width: '440px',
    });
    return (await firstValueFrom(ref.afterClosed())) === true;
  }

  private toast(message: string): void {
    this.snackBar.open(message, undefined, { duration: 4000 });
  }

  private messageOf(err: unknown): string {
    return err instanceof Error ? err.message : this.lang.t('settings.backup.unknownError');
  }
}
