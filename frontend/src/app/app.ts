import { Component } from '@angular/core';

import { RequestsPageComponent } from './requests/requests-page.component';

@Component({
  selector: 'app-root',
  imports: [RequestsPageComponent],
  templateUrl: './app.html',
})
export class App {}
