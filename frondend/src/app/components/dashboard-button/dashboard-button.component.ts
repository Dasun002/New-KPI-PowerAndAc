/* File: dashboard-button.component.ts
   Description: Dashboard navigation button component
   Purpose: Provides a navigation button to the main dashboard page.
*/

import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink, RouterLinkActive } from '@angular/router';

/* ========== DASHBOARD BUTTON COMPONENT ========== */

@Component({
  selector: 'app-dashboard-button',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterLinkActive],
  templateUrl: './dashboard-button.component.html',
  styleUrls: ['./dashboard-button.component.scss']
})
export class DashboardButtonComponent {}

