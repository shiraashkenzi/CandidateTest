import { Component, inject, input, output } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
} from '@angular/forms';

import {
  DEFAULT_SEARCH_PARAMS,
  REQUEST_STATUS_OPTIONS,
  REQUEST_TYPE_OPTIONS,
  RequestSearchParams,
  RequestStatus,
  RequestType,
  SORT_DIRECTION_OPTIONS,
  SORT_FIELD_OPTIONS,
  SortDirection,
  SortField,
} from './request.models';

/**
 * Rejects an inverted range. `YYYY-MM-DD` compares correctly as a string, so no Date parsing is
 * needed. Backend validation remains authoritative.
 */
export const dateRangeValidator: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  const from = group.get('createdFrom')?.value as string;
  const to = group.get('createdTo')?.value as string;
  return from && to && from > to ? { dateRange: true } : null;
};

/**
 * Owns the draft filter state only: nothing is applied until Search is submitted, and this component
 * never calls the API or knows about paging, results or errors.
 */
@Component({
  selector: 'app-request-filters',
  imports: [ReactiveFormsModule],
  templateUrl: './request-filters.component.html',
  styleUrl: './request-filters.component.css',
})
export class RequestFiltersComponent {
  private readonly fb = inject(NonNullableFormBuilder);

  /** Set while a request is in flight, to stop overlapping searches. */
  readonly disabled = input(false);

  readonly search = output<RequestSearchParams>();
  readonly clear = output<void>();

  readonly statusOptions = REQUEST_STATUS_OPTIONS;
  readonly typeOptions = REQUEST_TYPE_OPTIONS;
  readonly sortFieldOptions = SORT_FIELD_OPTIONS;
  readonly sortDirectionOptions = SORT_DIRECTION_OPTIONS;

  readonly form = this.fb.group(
    {
      requestNumber: [DEFAULT_SEARCH_PARAMS.requestNumber],
      statuses: [[...DEFAULT_SEARCH_PARAMS.statuses] as RequestStatus[]],
      requestType: [DEFAULT_SEARCH_PARAMS.requestType as RequestType | null],
      createdFrom: [DEFAULT_SEARCH_PARAMS.createdFrom],
      createdTo: [DEFAULT_SEARCH_PARAMS.createdTo],
      sortBy: [DEFAULT_SEARCH_PARAMS.sortBy as SortField],
      sortDirection: [DEFAULT_SEARCH_PARAMS.sortDirection as SortDirection],
    },
    { validators: dateRangeValidator },
  );

  get dateRangeInvalid(): boolean {
    return this.form.hasError('dateRange');
  }

  isStatusSelected(value: RequestStatus): boolean {
    return this.form.controls.statuses.value.includes(value);
  }

  /**
   * Managed by hand rather than through a FormArray so the control keeps holding a plain array of
   * selected values. Rebuilding from the option list keeps that array in a canonical order whatever
   * order the boxes are ticked in.
   */
  onStatusToggle(value: RequestStatus, checked: boolean): void {
    const selected = this.form.controls.statuses.value;

    const next = this.statusOptions
      .map((option) => option.value)
      .filter((option) => (option === value ? checked : selected.includes(option)));

    this.form.controls.statuses.setValue(next);
  }

  onSubmit(): void {
    if (this.form.invalid) {
      return;
    }

    this.search.emit(this.form.getRawValue());
  }

  onClear(): void {
    this.form.reset({
      ...DEFAULT_SEARCH_PARAMS,
      statuses: [...DEFAULT_SEARCH_PARAMS.statuses],
    });
    this.clear.emit();
  }
}
