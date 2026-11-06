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
import {
  AI_PROVIDERS,
  AiProvider,
  AppSettingsService,
  AppSettingsUpdate,
  AppSettingsView,
} from '../../shared/services/app-settings.service';

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
  readonly appSettingsApi = inject(AppSettingsService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly limits = BACKUP_LIMITS;
  readonly status = signal<BackupStatus | null>(null);
  readonly list = signal<BackupInfo[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  // AI provider and API keys. Secrets are write-only: the server only says whether one is saved.
  readonly providers = AI_PROVIDERS;
  readonly appSettings = signal<AppSettingsView | null>(null);
  aiProvider: AiProvider = 'local';
  aiModel = '';
  allowCloudData = false;
  anthropicKey = '';
  openAiKey = '';
  brapiToken = '';
  fredKey = '';

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
    void this.loadAppSettings();
  }

  async loadAppSettings(): Promise<void> {
    try {
      this.applyAppSettings(await this.appSettingsApi.get());
    } catch (err) {
      this.toast(this.messageOf(err));
    }
  }

  private applyAppSettings(view: AppSettingsView): void {
    this.appSettings.set(view);
    this.aiProvider = view.aiProvider;
    this.aiModel = view.aiModel ?? '';
    this.allowCloudData = view.allowCloudData;
    this.anthropicKey = '';
    this.openAiKey = '';
    this.brapiToken = '';
    this.fredKey = '';
  }

  providerKey(provider: AiProvider): string {
    return `settings.ai.provider.${provider}`;
  }

  /** True when the chosen cloud provider has no saved key and none is being typed. */
  missingCloudKey(): boolean {
    const view = this.appSettings();
    if (!view) return false;
    if (this.aiProvider === 'anthropic') return !view.anthropicKeyConfigured && !this.anthropicKey.trim();
    if (this.aiProvider === 'openai') return !view.openAiKeyConfigured && !this.openAiKey.trim();
    return false;
  }

  async saveAppSettings(): Promise<void> {
    await this.run(async () => {
      const update: AppSettingsUpdate = {
        aiProvider: this.aiProvider,
        aiModel: this.aiModel,
        allowCloudData: this.allowCloudData,
      };
      // Only send secrets the user actually typed; blank means "keep what is saved".
      if (this.anthropicKey.trim()) update.anthropicApiKey = this.anthropicKey.trim();
      if (this.openAiKey.trim()) update.openAiApiKey = this.openAiKey.trim();
      if (this.brapiToken.trim()) update.brapiToken = this.brapiToken.trim();
      if (this.fredKey.trim()) update.fredApiKey = this.fredKey.trim();
      this.applyAppSettings(await this.appSettingsApi.update(update));
      this.toast(this.lang.t('settings.app.saved'));
    });
  }

  async clearSecret(field: 'anthropicApiKey' | 'openAiApiKey' | 'brapiToken' | 'fredApiKey'): Promise<void> {
    const confirmed = await this.confirm({
      title: this.lang.t('settings.keys.clearTitle'),
      message: this.lang.t('settings.keys.clearMessage'),
      confirmLabel: this.lang.t('settings.keys.clear'),
      cancelLabel: this.lang.t('settings.cancel'),
      destructive: true,
    });
    if (!confirmed) return;
    await this.run(async () => {
      this.applyAppSettings(await this.appSettingsApi.update({ [field]: '' }));
      this.toast(this.lang.t('settings.keys.cleared'));
    });
  }

  async openBackupFolder(): Promise<void> {
    await this.run(async () => {
      await this.backups.openFolder();
      this.toast(this.lang.t('settings.backup.folderOpened'));
    });
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
