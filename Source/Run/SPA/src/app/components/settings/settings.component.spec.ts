import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import { BackupInfo, BackupService, BackupStatus } from '../../shared/services/backup.service';
import { SettingsComponent } from './settings.component';

const status: BackupStatus = {
  settings: { enabled: true, intervalDays: 15, maxBackups: 10 },
  databasePath: 'C:/data/wealthledger.db',
  backupDirectory: 'C:/data/Backups',
  backupCount: 1,
  lastBackupUtc: '2026-01-01T00:00:00Z',
  nextScheduledBackupUtc: '2026-01-16T00:00:00Z',
  restorePending: false,
};

const backup: BackupInfo = {
  fileName: 'wealthledger-20260101-000000-manual.db',
  createdUtc: '2026-01-01T00:00:00Z',
  sizeBytes: 2048,
  reason: 'manual',
};

describe('SettingsComponent', () => {
  let backups: jasmine.SpyObj<BackupService>;

  beforeEach(async () => {
    backups = jasmine.createSpyObj<BackupService>('BackupService', [
      'getStatus', 'list', 'updateSettings', 'createBackup', 'restore', 'remove', 'downloadUrl',
    ]);
    backups.getStatus.and.resolveTo(status);
    backups.list.and.resolveTo([backup]);
    backups.downloadUrl.and.callFake((name: string) => `/api/backups/${name}/download`);

    await TestBed.configureTestingModule({
      imports: [SettingsComponent],
      providers: [
        { provide: BackupService, useValue: backups },
        provideHttpClient(),
        provideHttpClientTesting(),
        provideNoopAnimations(),
      ],
    }).compileComponents();
  });

  function create() {
    const fixture = TestBed.createComponent(SettingsComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('accepts the documented ranges and rejects values outside them', () => {
    const component = create().componentInstance;

    component.intervalDays = 0;
    expect(component.isValid()).toBeFalse();

    component.intervalDays = 365;
    component.maxBackups = 100;
    expect(component.isValid()).toBeTrue();

    component.maxBackups = 101;
    expect(component.isValid()).toBeFalse();
  });

  it('formats byte sizes for humans', () => {
    const component = create().componentInstance;

    expect(component.formatBytes(512)).toBe('512 B');
    expect(component.formatBytes(2048)).toBe('2.0 KB');
    expect(component.formatBytes(5 * 1024 * 1024)).toBe('5.0 MB');
  });

  it('maps known backup reasons to translation keys and falls back to manual', () => {
    const component = create().componentInstance;

    expect(component.reasonKey('pre-upgrade')).toBe('settings.backup.reason.preUpgrade');
    expect(component.reasonKey('pre-import')).toBe('settings.backup.reason.preImport');
    expect(component.reasonKey('something-else')).toBe('settings.backup.reason.manual');
  });

  it('loads the status and renders one row per backup', async () => {
    const fixture = create();
    await fixture.whenStable();
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(1);
    expect(fixture.componentInstance.enabled).toBeTrue();
    expect(fixture.componentInstance.intervalDays).toBe(15);
  });

  it('does not restore when the confirmation is cancelled', async () => {
    const fixture = create();
    const dialog = TestBed.inject(MatDialog);
    spyOn(dialog, 'open').and.returnValue({ afterClosed: () => of(false) } as never);

    await fixture.componentInstance.restore(backup);

    expect(backups.restore).not.toHaveBeenCalled();
  });

  it('restores after confirmation and shows the staged status', async () => {
    const fixture = create();
    const dialog = TestBed.inject(MatDialog);
    spyOn(dialog, 'open').and.returnValue({ afterClosed: () => of(true) } as never);
    backups.restore.and.resolveTo({ ...status, restorePending: true });

    await fixture.componentInstance.restore(backup);

    expect(backups.restore).toHaveBeenCalledWith(backup.fileName);
    expect(fixture.componentInstance.status()?.restorePending).toBeTrue();
  });

  it('does not save an invalid policy', async () => {
    const component = create().componentInstance;
    component.maxBackups = 0;

    await component.saveSettings();

    expect(backups.updateSettings).not.toHaveBeenCalled();
  });
});
