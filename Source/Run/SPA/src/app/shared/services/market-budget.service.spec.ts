import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { MarketToolsService, sparklinePoints } from './market-tools.service';
import { BudgetService } from './budget.service';

describe('sparklinePoints', () => {
  it('returns nothing for fewer than two values', () => {
    expect(sparklinePoints([], 100, 20)).toBe('');
    expect(sparklinePoints([5], 100, 20)).toBe('');
  });

  it('maps the lowest value to the bottom and the highest to the top', () => {
    const points = sparklinePoints([10, 20], 100, 20, 2).split(' ');
    expect(points[0]).toBe('2.0,18.0'); // min: left, bottom
    expect(points[1]).toBe('98.0,2.0'); // max: right, top
  });

  it('does not divide by zero for a flat series', () => {
    const points = sparklinePoints([7, 7, 7], 100, 20);
    expect(points).not.toContain('NaN');
  });
});

describe('MarketToolsService and BudgetService', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('unwraps the treasury snapshot', async () => {
    const pending = TestBed.inject(MarketToolsService).treasury();
    http.expectOne('/api/market-tools/treasury').flush({
      data: { baseDate: '2026-11-05', bonds: [], fetchedAtUtc: '2026-11-06T00:00:00Z', fromCache: false },
      errors: [],
    });
    expect((await pending).baseDate).toBe('2026-11-05');
  });

  it('surfaces a provider outage as the server message', async () => {
    const pending = TestBed.inject(MarketToolsService).indices();
    http.expectOne('/api/market-tools/indices').flush(
      { data: null, errors: [{ message: 'Market index data is unavailable right now.' }] },
      { status: 502, statusText: 'Bad Gateway' },
    );
    await expectAsync(pending).toBeRejectedWithError('Market index data is unavailable right now.');
  });

  it('requests the budget status for a specific month', async () => {
    const pending = TestBed.inject(BudgetService).status(2026, 11);
    const req = http.expectOne(r => r.url === '/api/budgets/status');
    expect(req.request.params.get('year')).toBe('2026');
    expect(req.request.params.get('month')).toBe('11');
    req.flush({ data: { year: 2026, month: 11, items: [], unbudgeted: [], totalLimit: 0, totalSpent: 0 }, errors: [] });
    expect((await pending).month).toBe(11);
  });

  it('saves with PUT and the category and limit in the body', async () => {
    const pending = TestBed.inject(BudgetService).save('Food', 800);
    const req = http.expectOne('/api/budgets');
    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({ category: 'Food', monthlyLimit: 800 });
    req.flush({ data: {}, errors: [] });
    await pending;
  });
});
