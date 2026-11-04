/* File: header-title.component.ts
   Description: Page header title component
   Purpose: Displays the current page title in the application header.
   Inputs: title (string) - page title to display
*/

import { Component, Input } from '@angular/core';

/* ========== HEADER TITLE COMPONENT ========== */

@Component({
  selector: 'app-header-title',
  standalone: true,
  imports: [],
  templateUrl: './header-title.component.html',
  styleUrls: ['./header-title.component.scss']
})
export class HeaderTitleComponent {
  /* Page title to display */
  @Input() title: string = 'KPI Dashboard';
}

