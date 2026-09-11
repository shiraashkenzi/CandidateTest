import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';

import {
  CurrentUser,
  DEFAULT_SEARCH_PARAMS,
  PagedResult,
  RequestDto,
  RequestSearchParams,
} from './request.models';
import { RequestsApiService, RequestSearchQuery } from './requests-api.service';
import { RequestsPageComponent } from './requests-page.component';

/** Captures what the page actually asked the API for. */
class StubApiService {
  readonly calls: { query: RequestSearchQuery; identity: CurrentUser }[] = [];
  totalCount = 100;

  searchRequests(query: RequestSearchQuery, identity: CurrentUser): Observable<PagedResult<RequestDto>> {
    this.calls.push({ query, identity });
    return of({ items: [], totalCount: this.totalCount, page: query.page, pageSize: query.pageSize });
  }

  get lastCall() {
    return this.calls[this.calls.length - 1];
  }
}

describe('RequestsPageComponent', () => {
  let fixture: ComponentFixture<RequestsPageComponent>;
  let component: RequestsPageComponent;
  let api: StubApiService;

  const criteria = (overrides: Partial<RequestSearchParams> = {}): RequestSearchParams => ({
    ...DEFAULT_SEARCH_PARAMS,
    ...overrides,
  });

  beforeEach(async () => {
    api = new StubApiService();

    await TestBed.configureTestingModule({
      imports: [RequestsPageComponent],
      providers: [{ provide: RequestsApiService, useValue: api }],
    }).compileComponents();

    fixture = TestBed.createComponent(RequestsPageComponent);
    component = fixture.componentInstance;
    fixture.detectChanges(); // triggers the initial search
  });

  it('loads the default result set once on init', () => {
    expect(api.calls.length).toBe(1);
    expect(api.lastCall.query.page).toBe(1);
    expect(api.lastCall.query.pageSize).toBe(20);
    expect(api.lastCall.query.sortBy).toBe('createdAt');
    expect(api.lastCall.query.sortDirection).toBe('desc');
    expect(api.lastCall.identity).toEqual({ userId: 3, isAdmin: false });
  });

  it('resets to page 1 when a new search is submitted', () => {
    component.onNextPage();
    component.onNextPage();
    expect(component.page()).toBe(3);

    component.onSearch(criteria({ statuses: [1] }));

    expect(component.page()).toBe(1);
    expect(api.lastCall.query.page).toBe(1);
  });

  it('reuses the applied criteria and applied identity when paging', () => {
    component.onSearch(criteria({ statuses: [2], requestNumber: 'REQ-1' }));

    // The user edits the identity but does NOT press Search — this must not affect paging.
    component.onUserIdChange('5');
    component.onIsAdminChange(true);

    component.onNextPage();

    expect(component.page()).toBe(2);
    expect(api.lastCall.query.statuses).toEqual([2]);
    expect(api.lastCall.query.requestNumber).toBe('REQ-1');
    expect(api.lastCall.identity).toEqual({ userId: 3, isAdmin: false });

    // Pressing Search commits the edited identity.
    component.onSearch(criteria());
    expect(api.lastCall.identity).toEqual({ userId: 5, isAdmin: true });
  });

  it('clears the criteria and reloads from page 1', () => {
    component.onSearch(criteria({ statuses: [3], requestNumber: 'REQ-9', sortBy: 'status' }));
    component.onNextPage();
    expect(component.page()).toBe(2);

    component.onClear();

    expect(component.page()).toBe(1);
    expect(component.criteria()).toEqual(DEFAULT_SEARCH_PARAMS);
    expect(api.lastCall.query.statuses).toEqual([]);
    expect(api.lastCall.query.requestNumber).toBe('');
    expect(api.lastCall.query.sortBy).toBe('createdAt');
    expect(api.lastCall.query.page).toBe(1);
  });

  it('resets to page 1 on a page-size change, keeping applied filters and identity', () => {
    component.onUserIdChange('7');
    component.onIsAdminChange(true);
    component.onSearch(criteria({ statuses: [2], requestNumber: 'REQ-1' }));
    component.onNextPage();
    expect(component.page()).toBe(2);

    // Draft identity is edited again but not submitted — the page-size change must not pick it up.
    component.onUserIdChange('9');

    component.onPageSizeChange('50');

    expect(component.page()).toBe(1);
    expect(api.lastCall.query.pageSize).toBe(50);
    expect(api.lastCall.query.page).toBe(1);
    expect(api.lastCall.query.statuses).toEqual([2]);
    expect(api.lastCall.query.requestNumber).toBe('REQ-1');
    expect(api.lastCall.identity).toEqual({ userId: 7, isAdmin: true });
  });

  it('shows the page size that is actually in use', () => {
    // Guards the binding bug where [value] on the <select> ran before @for created the options,
    // leaving the control showing "10" while the page was really using 20.
    fixture.detectChanges();
    const select = fixture.nativeElement.querySelector('#pageSize') as HTMLSelectElement;
    expect(select.value).toBe('20');

    component.onPageSizeChange('100');
    fixture.detectChanges();
    expect(select.value).toBe('100');
  });

  it('hides pagination and shows the no-results state on an empty result set', () => {
    api.totalCount = 0;
    component.onSearch(criteria());

    expect(component.showPagination()).toBeFalse();
    expect(component.showNoResults()).toBeTrue();
  });
});
