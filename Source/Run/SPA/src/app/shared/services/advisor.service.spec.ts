import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { AdvisorService } from './advisor.service';

describe('AdvisorService', () => {
  let service: AdvisorService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(AdvisorService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts the question with scope, language and history, and unwraps the answer', async () => {
    const pending = service.ask({
      question: 'Where should I invest?',
      scope: 'portfolio',
      language: 'en',
      history: [{ role: 'user', content: 'hi' }],
    });

    const req = http.expectOne('/api/advisor/ask');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.scope).toBe('portfolio');
    expect(req.request.body.history.length).toBe(1);
    req.flush({
      data: { answer: 'Tesouro Selic', provider: 'anthropic', model: 'claude-opus-5-5', usedUserData: false },
      errors: [],
    });

    const answer = await pending;
    expect(answer.answer).toBe('Tesouro Selic');
    expect(answer.usedUserData).toBeFalse();
  });

  it('requests the browser-model context with scope and language as query params', async () => {
    const pending = service.context('expenses', 'pt');

    const req = http.expectOne(r => r.url === '/api/advisor/context');
    expect(req.request.params.get('scope')).toBe('expenses');
    expect(req.request.params.get('language')).toBe('pt');
    req.flush({ data: { systemPrompt: 'sys', dataSummary: 'sum' }, errors: [] });

    expect((await pending).dataSummary).toBe('sum');
  });

  it('surfaces the server error message', async () => {
    const pending = service.ask({ question: 'x', scope: 'general', language: 'en' });

    http.expectOne('/api/advisor/ask').flush(
      { data: null, errors: [{ message: 'The AI provider rejected the API key.' }] },
      { status: 502, statusText: 'Bad Gateway' },
    );

    await expectAsync(pending).toBeRejectedWithError('The AI provider rejected the API key.');
  });
});
