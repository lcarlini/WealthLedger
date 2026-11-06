import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { BackupService } from './backup.service';

describe('BackupService', () => {
  let service: BackupService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(BackupService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('unwraps the data envelope for the status call', async () => {
    const pending = service.getStatus();

    http.expectOne('/api/backups/status').flush({ data: { backupCount: 2 }, errors: [] });

    expect((await pending).backupCount).toBe(2);
  });

  it('returns an empty list when the server sends no data', async () => {
    const pending = service.list();

    http.expectOne('/api/backups').flush({ data: null, errors: [] });

    expect(await pending).toEqual([]);
  });

  it('sends the policy as a PUT to the settings endpoint', async () => {
    const pending = service.updateSettings({ enabled: true, intervalDays: 15, maxBackups: 10 });

    const req = http.expectOne('/api/backups/settings');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ enabled: true, intervalDays: 15, maxBackups: 10 });
    req.flush({ data: { enabled: true, intervalDays: 15, maxBackups: 10 }, errors: [] });

    expect((await pending).maxBackups).toBe(10);
  });

  it('surfaces the server message instead of a generic HTTP error', async () => {
    const pending = service.restore('wealthledger-20260101-000000-manual.db');

    http
      .expectOne('/api/backups/wealthledger-20260101-000000-manual.db/restore')
      .flush({ data: null, errors: [{ message: 'Backup not found.' }] }, { status: 404, statusText: 'Not Found' });

    await expectAsync(pending).toBeRejectedWithError('Backup not found.');
  });

  it('encodes file names in download links', () => {
    expect(service.downloadUrl('my backup.db')).toBe('/api/backups/my%20backup.db/download');
  });
});
