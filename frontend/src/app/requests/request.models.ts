/**
 * Types and constants for the Requests screen.
 *
 * Enums are modelled as `as const` option arrays rather than TypeScript `enum`s, for three reasons:
 *  - a numeric `enum` accepts *any* number without a type error, which is wrong for a fixed contract;
 *  - the dropdowns need ordered {value, label} lists anyway, so this is the single source for both
 *    the options and the display labels — adding a status means editing exactly one line;
 *  - `as const` plus a derived union narrows more strictly than a numeric enum.
 *
 * The backend serializes these enums as integers; that contract is unchanged.
 */

// ── Enum contracts (must match the backend) ─────────────────────────────────

export const REQUEST_STATUS_OPTIONS = [
  { value: 1, label: 'New' },
  { value: 2, label: 'In Progress' },
  { value: 3, label: 'Completed' },
  { value: 4, label: 'Cancelled' },
] as const;

export type RequestStatus = (typeof REQUEST_STATUS_OPTIONS)[number]['value'];

export const REQUEST_TYPE_OPTIONS = [
  { value: 1, label: 'General' },
  { value: 2, label: 'Legal' },
  { value: 3, label: 'Payment' },
  { value: 4, label: 'Appeal' },
] as const;

export type RequestType = (typeof REQUEST_TYPE_OPTIONS)[number]['value'];

// ── Sorting (whitelist mirrors the backend; `id` is deliberately not offered) ─

export const SORT_FIELD_OPTIONS = [
  { value: 'createdAt', label: 'Created At' },
  { value: 'requestNumber', label: 'Request Number' },
  { value: 'status', label: 'Status' },
  { value: 'requestType', label: 'Request Type' },
] as const;

export type SortField = (typeof SORT_FIELD_OPTIONS)[number]['value'];

export const SORT_DIRECTION_OPTIONS = [
  { value: 'desc', label: 'Newest first' },
  { value: 'asc', label: 'Oldest first' },
] as const;

export type SortDirection = (typeof SORT_DIRECTION_OPTIONS)[number]['value'];

/** Matches the backend's maximum page size of 100, so the UI cannot generate a 400. */
export const PAGE_SIZE_OPTIONS = [10, 20, 50, 100] as const;

export const DEFAULT_PAGE_SIZE = 20;

// ── API shapes ──────────────────────────────────────────────────────────────

export interface RequestDto {
  readonly id: number;
  readonly requestNumber: string;
  readonly customerId: number;
  readonly ownerId: number;
  /** Null when the request is unassigned. */
  readonly assignedToUserId: number | null;
  readonly status: RequestStatus;
  readonly requestType: RequestType;
  /** ISO 8601 string — HttpClient does not revive dates. */
  readonly createdAt: string;
}

export interface PagedResult<T> {
  readonly items: T[];
  readonly totalCount: number;
  readonly page: number;
  readonly pageSize: number;
}

/** The filter/sort values a user can submit. Paging is owned by the page component. */
export interface RequestSearchParams {
  requestNumber: string;
  statuses: readonly RequestStatus[];
  requestType: RequestType | null;
  /** `YYYY-MM-DD`, sent to the backend verbatim. */
  createdFrom: string;
  /** `YYYY-MM-DD`, sent to the backend verbatim. */
  createdTo: string;
  sortBy: SortField;
  sortDirection: SortDirection;
}

/**
 * Exercise-only identity. There is no login, token or session: the backend contract for this
 * assignment reads `X-User-Id` / `X-Is-Admin` headers. Authorization itself is enforced server-side.
 */
export interface CurrentUser {
  userId: number;
  isAdmin: boolean;
}

export const DEFAULT_SEARCH_PARAMS: RequestSearchParams = {
  requestNumber: '',
  statuses: [],
  requestType: null,
  createdFrom: '',
  createdTo: '',
  sortBy: 'createdAt',
  sortDirection: 'desc',
};

export const DEFAULT_IDENTITY: CurrentUser = { userId: 3, isAdmin: false };

// ── Display labels, derived from the option lists above ─────────────────────

const STATUS_LABELS = new Map<number, string>(REQUEST_STATUS_OPTIONS.map((o) => [o.value, o.label]));
const TYPE_LABELS = new Map<number, string>(REQUEST_TYPE_OPTIONS.map((o) => [o.value, o.label]));

export function statusLabel(value: number): string {
  return STATUS_LABELS.get(value) ?? `Unknown (${value})`;
}

export function requestTypeLabel(value: number): string {
  return TYPE_LABELS.get(value) ?? `Unknown (${value})`;
}
