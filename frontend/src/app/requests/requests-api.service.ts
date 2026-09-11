import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { CurrentUser, PagedResult, RequestDto, RequestSearchParams } from './request.models';

/** Criteria plus the paging the page component owns. */
export type RequestSearchQuery = RequestSearchParams & {
  page: number;
  pageSize: number;
};

/**
 * The only place in the app that talks HTTP. Stateless by design: it builds the request, returns the
 * typed response, and lets the caller decide what success and failure mean for the UI.
 */
@Injectable({ providedIn: 'root' })
export class RequestsApiService {
  /**
   * Relative on purpose. In development the Angular dev-server proxy forwards `/api` to
   * http://localhost:60702; in any other environment it resolves against whatever origin serves the
   * app. No backend URL is hardcoded anywhere in the frontend.
   */
  static readonly ENDPOINT = '/api/requests';

  private readonly http = inject(HttpClient);

  searchRequests(query: RequestSearchQuery, identity: CurrentUser): Observable<PagedResult<RequestDto>> {
    let params = new HttpParams();

    // Optional filters are omitted entirely when empty, so the backend applies no predicate.
    const requestNumber = query.requestNumber.trim();
    if (requestNumber) {
      params = params.set('requestNumber', requestNumber);
    }

    // Repeated keys (?statuses=1&statuses=2). A comma-joined value would fail model binding
    // on the backend with a 400.
    for (const status of query.statuses) {
      params = params.append('statuses', status);
    }

    if (query.requestType !== null) {
      params = params.set('requestType', query.requestType);
    }

    // Sent verbatim as YYYY-MM-DD. Deliberately not passed through Date/toISOString, which would
    // apply the browser's timezone offset and can shift the day. The backend owns the range
    // semantics, including widening a date-only upper bound to cover the whole day.
    if (query.createdFrom) {
      params = params.set('createdFrom', query.createdFrom);
    }
    if (query.createdTo) {
      params = params.set('createdTo', query.createdTo);
    }

    params = params
      .set('sortBy', query.sortBy)
      .set('sortDirection', query.sortDirection)
      .set('page', query.page)
      .set('pageSize', query.pageSize);

    const headers = new HttpHeaders({
      'X-User-Id': String(identity.userId),
      'X-Is-Admin': String(identity.isAdmin),
    });

    return this.http.get<PagedResult<RequestDto>>(RequestsApiService.ENDPOINT, { params, headers });
  }
}
