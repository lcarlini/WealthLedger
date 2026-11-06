import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TradeService } from './trade.service';
import { TradeRequest, TradeSide } from '../../models/trade';

describe('TradeService', () => {
  let service: TradeService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(TradeService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists trades for a holding using the investmentId query parameter', async () => {
    const pending = service.list('inv-1');

    const req = http.expectOne(r => r.url === '/api/trades' && r.params.get('investmentId') === 'inv-1');
    expect(req.request.method).toBe('GET');
    req.flush({ data: [{ id: 't1' }], errors: [] });

    expect((await pending).length).toBe(1);
  });

  it('returns an empty list when the server sends no data', async () => {
    const pending = service.list('inv-1');

    http.expectOne(r => r.url === '/api/trades').flush({ data: null, errors: [] });

    expect(await pending).toEqual([]);
  });

  it('asks for the FIFO comparison only when requested', () => {
    service.position('inv-1');
    service.position('inv-1', 'fifo');

    const requests = http.match(r => r.url === '/api/trades/position/inv-1');
    expect(requests.map(r => r.request.params.get('method'))).toEqual(['weighted', 'fifo']);
    requests.forEach(r => r.flush({ data: null, errors: [] }));
  });

  it('posts a dividend with zero quantity and the per-share amount as the price', async () => {
    const body: TradeRequest = {
      investmentId: 'inv-1',
      side: TradeSide.Dividend,
      tradeDate: '2026-02-01',
      quantity: 0,
      unitPrice: 0.5,
      fees: 0,
      notes: null,
    };
    const pending = service.add(body);

    const req = http.expectOne('/api/trades');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual(body);
    req.flush({ data: { id: 't2' }, errors: [] });

    expect((await pending).id).toBe('t2');
  });

  it('shows the server message for a rejected trade instead of a generic HTTP error', async () => {
    const pending = service.add({
      investmentId: 'inv-1',
      side: TradeSide.Sell,
      tradeDate: '2026-02-01',
      quantity: 99,
      unitPrice: 10,
      fees: 0,
      notes: null,
    });

    http.expectOne('/api/trades').flush(
      { data: null, errors: [{ message: 'Selling 99 on 2026-02-01 exceeds the 10 held at that date.' }] },
      { status: 400, statusText: 'Bad Request' },
    );

    await expectAsync(pending).toBeRejectedWithError(/exceeds the 10 held/);
  });

  it('deletes a trade by id', async () => {
    const pending = service.remove('t9');

    const req = http.expectOne('/api/trades/t9');
    expect(req.request.method).toBe('DELETE');
    req.flush({ data: true, errors: [] });

    expect(await pending).toBeTrue();
  });

  it('requests the tax report for a year', () => {
    service.taxReport(2026);

    const req = http.expectOne(r => r.url === '/api/trades/tax-report');
    expect(req.request.params.get('year')).toBe('2026');
    req.flush({ data: null, errors: [] });
  });
});
