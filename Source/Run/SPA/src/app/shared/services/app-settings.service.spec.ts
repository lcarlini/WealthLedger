import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AppSettingsService } from './app-settings.service';
import { BackupService } from './backup.service';

describe('AppSettingsService', () => {
  let service: AppSettingsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(AppSettingsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('unwraps the data envelope', async () => {
    const pending = service.get();

    http.expectOne('/api/settings').flush({
      data: { aiProvider: 'local', aiModel: null, allowCloudData: false, anthropicKeyConfigured: true },
      errors: [],
    });

    const view = await pending;
    expect(view.aiProvider).toBe('local');
    expect(view.anthropicKeyConfigured).toBeTrue();
  });

  it('sends a partial update with PUT and surfaces server errors', async () => {
    const pending = service.update({ aiProvider: 'openai', openAiApiKey: 'k' });

    const req = http.expectOne('/api/settings');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ aiProvider: 'openai', openAiApiKey: 'k' });
    req.flush({ data: null, errors: [{ message: 'Unknown AI provider' }] }, { status: 400, statusText: 'Bad Request' });

    await expectAsync(pending).toBeRejectedWithError('Unknown AI provider');
  });
});

describe('BackupService.openFolder', () => {
  it('posts to the open-folder endpoint', async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const backups = TestBed.inject(BackupService);
    const http = TestBed.inject(HttpTestingController);

    const pending = backups.openFolder();
    const req = http.expectOne('/api/backups/open-folder');
    expect(req.request.method).toBe('POST');
    req.flush({ data: { backupCount: 1 }, errors: [] });

    expect((await pending).backupCount).toBe(1);
    http.verify();
  });
});
