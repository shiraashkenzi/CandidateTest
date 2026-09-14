import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';

import { RequestDto, requestTypeLabel, statusLabel } from './request.models';

/** Presentation only: no search state, no API calls. */
@Component({
  selector: 'app-requests-table',
  imports: [DatePipe],
  templateUrl: './requests-table.component.html',
  styleUrl: './requests-table.component.css',
})
export class RequestsTableComponent {
  readonly requests = input.required<RequestDto[]>();

  /**
   * True only when a search has completed successfully with zero matches, so the message cannot
   * appear before the first search or while one is running.
   */
  readonly showNoResults = input(false);

  protected readonly statusLabel = statusLabel;
  protected readonly requestTypeLabel = requestTypeLabel;
}
