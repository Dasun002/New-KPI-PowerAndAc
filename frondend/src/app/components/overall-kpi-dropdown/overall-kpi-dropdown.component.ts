/*
 File: overall-kpi-dropdown.component.ts
 Description: Overall KPI dropdown menu component
 Purpose: Navigation dropdown for Overall KPI section pages.
*/

import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavOption } from '../../page-config';

/* ========== OVERALL KPI DROPDOWN COMPONENT ========== */

@Component({
  selector: 'app-overall-kpi-dropdown',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './overall-kpi-dropdown.component.html',
  styleUrls: ['./overall-kpi-dropdown.component.scss']
})
export class OverallKpiDropdownComponent {
  /* Input: Overall KPI navigation options */
  @Input() options: NavOption[] = [];
  /* Output: Emit menu close request */
  @Output() closeMenu = new EventEmitter<void>();
  /* Output: Emit selected route path */
  @Output() selection = new EventEmitter<string>();

  constructor(private router: Router) {}

  /* Navigate to first available option and close menu */
  /* Navigate to first available option and close menu */
  onNavigate(): void {
    const target = this.options[0]?.path;
    if (!target) {
      return;
    }

    this.router.navigate([target]).then(() => {
      this.closeMenu.emit();
      this.selection.emit(target);
    }).catch((error) => {
      console.error('Navigation error:', error);
      this.closeMenu.emit();
    });
  }
}

