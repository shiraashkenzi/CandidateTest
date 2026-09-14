import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';

import { RequestFiltersComponent } from './request-filters.component';
import {
  CurrentUser,
  DEFAULT_IDENTITY,
  DEFAULT_PAGE_SIZE,
  DEFAULT_SEARCH_PARAMS,
  PAGE_SIZE_OPTIONS,
  RequestDto,
  RequestSearchParams,
} from './request.models';
import { RequestsApiService } from './requests-api.service';
import { RequestsTableComponent } from './requests-table.component';

/** Shape of an RFC 7807 problem response, as far as this screen cares. */
interface ProblemDetails {
  errors?: Record<string, string[]>;
  detail?: string;
  title?: string;
}

/**
 * Orchestrates the screen and owns every piece of applied state. Editing the filter form or the
 * identity inputs changes draft state only; Search and Clear commit it, and pagination reuses
 * whatever was last committed, so paging can never switch filters or user mid-way.
 */
@Component({
  selector: 'app-requests-page',
  imports: [RequestFiltersComponent, RequestsTableComponent],
  templateUrl: './requests-page.component.html',
  styleUrl: './requests-page.component.css',
})
export class RequestsPageComponent implements OnInit {
  private readonly api = inject(RequestsApiService);

  // ── Draft identity: bound to the inputs, committed only by Search / Clear ──
  readonly draftUserId = signal(DEFAULT_IDENTITY.userId);
  readonly draftIsAdmin = signal(DEFAULT_IDENTITY.isAdmin);

  // ── Applied state: what every request is actually built from ──────────────
  readonly appliedIdentity = signal<CurrentUser>({ ...DEFAULT_IDENTITY });
  readonly criteria = signal<RequestSearchParams>({ ...DEFAULT_SEARCH_PARAMS });

  readonly requests = signal<RequestDto[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal<number>(DEFAULT_PAGE_SIZE);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly hasSearched = signal(false);

  readonly pageSizeOptions = PAGE_SIZE_OPTIONS;

  // ── Derived ───────────────────────────────────────────────────────────────
  readonly identityValid = computed(
    () => Number.isInteger(this.draftUserId()) && this.draftUserId() > 0,
  );
  readonly totalPages = computed(() => Math.ceil(this.totalCount() / this.pageSize()));
  readonly canPrevious = computed(() => !this.loading() && this.page() > 1);
  readonly canNext = computed(() => !this.loading() && this.page() < this.totalPages());
  /** Pagination is hidden entirely on an empty result set, so no "Page 1 of 0" can appear. */
  readonly showPagination = computed(() => this.totalCount() > 0);
  readonly showNoResults = computed(
    () => this.hasSearched() && !this.loading() && !this.error() && this.requests().length === 0,
  );

  ngOnInit(): void {
    this.runSearch();
  }

  // ── Identity inputs (draft only — these never fetch) ──────────────────────

  onUserIdChange(value: string): void {
    this.draftUserId.set(value.trim() === '' ? Number.NaN : Number(value));
  }

  onIsAdminChange(checked: boolean): void {
    this.draftIsAdmin.set(checked);
  }

  // ── Commands ──────────────────────────────────────────────────────────────

  onSearch(params: RequestSearchParams): void {
    if (!this.identityValid()) {
      return;
    }

    this.criteria.set(params);
    this.commitIdentity();
    this.page.set(1);
    this.runSearch();
  }

  onClear(): void {
    if (!this.identityValid()) {
      return;
    }

    this.criteria.set({ ...DEFAULT_SEARCH_PARAMS });
    this.commitIdentity();
    this.page.set(1);
    this.runSearch();
  }

  onPreviousPage(): void {
    if (!this.canPrevious()) {
      return;
    }

    this.page.update((page) => page - 1);
    this.runSearch();
  }

  onNextPage(): void {
    if (!this.canNext()) {
      return;
    }

    this.page.update((page) => page + 1);
    this.runSearch();
  }

  onPageSizeChange(value: string): void {
    this.pageSize.set(Number(value));
    this.page.set(1);
    this.runSearch();
  }

  dismissError(): void {
    this.error.set(null);
  }

  // ── Internals ─────────────────────────────────────────────────────────────

  private commitIdentity(): void {
    this.appliedIdentity.set({ userId: this.draftUserId(), isAdmin: this.draftIsAdmin() });
  }

  private runSearch(): void {
    this.loading.set(true);
    this.error.set(null);

    const query = { ...this.criteria(), page: this.page(), pageSize: this.pageSize() };

    this.api.searchRequests(query, this.appliedIdentity()).subscribe({
      next: (result) => {
        this.requests.set(result.items);
        this.totalCount.set(result.totalCount);
        this.hasSearched.set(true);
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        // Previous results stay on screen; the banner explains what failed.
        this.error.set(this.describeError(err));
        this.hasSearched.set(true);
        this.loading.set(false);
      },
    });
  }

  /**
   * Maps a failed response to one friendly sentence; raw bodies are never shown. For a 400 only the
   * *values* of the RFC 7807 `errors` object are rendered — they are already readable sentences, so
   * the backend's PascalCase keys never need mapping back to form control names.
   */
  private describeError(err: HttpErrorResponse): string {
    if (err.status === 0) {
      return 'Could not reach the API. Check that the backend is running on http://localhost:60702.';
    }

    if (err.status === 401) {
      return 'A valid User ID is required. Enter a User ID greater than zero, then search again.';
    }

    if (err.status === 400) {
      const problem = err.error as ProblemDetails | null;
      const messages = problem?.errors ? Object.values(problem.errors).flat() : [];

      if (messages.length > 0) {
        return messages.join(' ');
      }

      return problem?.detail ?? problem?.title ?? 'The search request was rejected.';
    }

    return 'Unable to load requests. Please try again.';
  }
}
