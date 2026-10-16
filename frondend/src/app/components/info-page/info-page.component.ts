/* File: info-page.component.ts
   Description: Generic information page component
   Purpose: Displays dynamic page content loaded from route data.
   Supports pages with title and description from routing configuration.
*/

import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { ActivatedRoute } from '@angular/router';

/* ========== INFO PAGE COMPONENT ========== */

@Component({
  selector: 'app-info-page',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './info-page.component.html',
  styleUrls: ['./info-page.component.scss']
})
export class InfoPageComponent {
  /* Page title from route data */
  protected readonly pageTitle: string;
  /* Page description from route data */
  protected readonly description: string;

  constructor(private readonly route: ActivatedRoute) {
    /* Read title from route data, default to 'Details' */
    this.pageTitle = this.route.snapshot.data['title'] ?? 'Details';
    /* Read description from route data, default to generic message */
    this.description =
      this.route.snapshot.data['description'] ??
      'Content for this section will be available soon.';
  }
}

