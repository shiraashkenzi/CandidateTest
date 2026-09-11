import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { DEFAULT_PAGE_SIZE, DEFAULT_SEARCH_PARAMS } from './request.models';
import { RequestsApiService } from './requests-api.service';

describe('RequestsApiService', () => {
  let service: RequestsApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    service = TestBed.inject(RequestsApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('sends repeated statuses keys and omits empty optional filters', () => {
    service
      .searchRequests(
        {
          ...DEFAULT_SEARCH_PARAMS,
          statuses: [1, 2],
          page: 1,
          pageSize: DEFAULT_PAGE_SIZE,
        },
        { userId: 3, isAdmin: false },
      )
      .subscribe();

    const req = httpMock.expectOne((r) => r.url === '/api/requests');

    // Repeated keys, never a comma-joined value: the backend binds RequestStatus[] and
    // "?statuses=1,2" would fail model binding with a 400.
    expect(req.request.params.getAll('statuses')).toEqual(['1', '2']);
    expect(req.request.urlWithParams).toContain('statuses=1&statuses=2');
    expect(req.request.urlWithParams).not.toContain('statuses=1,2');

    // Blank optional filters are omitted entirely rather than sent as empty values.
    expect(req.request.params.has('requestNumber')).toBeFalse();
    expect(req.request.params.has('requestType')).toBeFalse();
    expect(req.request.params.has('createdFrom')).toBeFalse();
    expect(req.request.params.has('createdTo')).toBeFalse();

    // Sorting and paging are always present.
    expect(req.request.params.get('sortBy')).toBe('createdAt');
    expect(req.request.params.get('sortDirection')).toBe('desc');
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('20');

    req.flush({ items: [], totalCount: 0, page: 1, pageSize: 20 });
  });

  it('sends the exercise identity headers and calls the relative endpoint', () => {
    service
      .searchRequests(
        { ...DEFAULT_SEARCH_PARAMS, page: 2, pageSize: 50 },
        { userId: 7, isAdmin: true },
      )
      .subscribe();

    const req = httpMock.expectOne((r) => r.url === '/api/requests');

    expect(req.request.headers.get('X-User-Id')).toBe('7');
    expect(req.request.headers.get('X-Is-Admin')).toBe('true');
    // Relative URL, so the dev proxy handles it and no backend host is hardcoded.
    expect(req.request.url).toBe('/api/requests');

    req.flush({ items: [], totalCount: 0, page: 2, pageSize: 50 });
  });
});
