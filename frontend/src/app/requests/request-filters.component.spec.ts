import { ComponentFixture, TestBed } from '@angular/core/testing';

import { RequestFiltersComponent } from './request-filters.component';
import { RequestSearchParams } from './request.models';

describe('RequestFiltersComponent', () => {
  let fixture: ComponentFixture<RequestFiltersComponent>;
  let component: RequestFiltersComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [RequestFiltersComponent] }).compileComponents();

    fixture = TestBed.createComponent(RequestFiltersComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('rejects createdFrom later than createdTo and emits nothing', () => {
    const emitted: RequestSearchParams[] = [];
    component.search.subscribe((params) => emitted.push(params));

    component.form.patchValue({ createdFrom: '2026-06-01', createdTo: '2026-01-01' });
    component.onSubmit();

    expect(component.form.invalid).toBeTrue();
    expect(component.dateRangeInvalid).toBeTrue();
    expect(emitted).toEqual([]);

    // The same date on both ends is a valid single-day range and must submit.
    component.form.patchValue({ createdFrom: '2026-01-15', createdTo: '2026-01-15' });
    component.onSubmit();

    expect(component.form.valid).toBeTrue();
    expect(emitted.length).toBe(1);
    expect(emitted[0].createdFrom).toBe('2026-01-15');
    expect(emitted[0].createdTo).toBe('2026-01-15');
  });

  it('supports zero, one and multiple statuses via the checkbox group', () => {
    const emitted: RequestSearchParams[] = [];
    component.search.subscribe((params) => emitted.push(params));

    const checkboxes: HTMLInputElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('.statuses__option input[type="checkbox"]'),
    );
    expect(checkboxes.length).toBe(4);
    expect(checkboxes.every((box) => !box.checked)).toBeTrue();

    // Zero selected.
    component.onSubmit();
    expect(emitted[0].statuses).toEqual([]);

    // One selected.
    component.onStatusToggle(2, true);
    component.onSubmit();
    expect(emitted[1].statuses).toEqual([2]);

    // Multiple — ticked out of order, but stored in canonical option order.
    component.onStatusToggle(1, true);
    component.onSubmit();
    expect(emitted[2].statuses).toEqual([1, 2]);
    expect(component.isStatusSelected(1)).toBeTrue();

    // Unticking removes only that value.
    component.onStatusToggle(2, false);
    component.onSubmit();
    expect(emitted[3].statuses).toEqual([1]);
    expect(component.isStatusSelected(2)).toBeFalse();

    // Clear resets the group.
    component.onClear();
    expect(component.form.getRawValue().statuses).toEqual([]);
  });
});
