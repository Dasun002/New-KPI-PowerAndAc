/*
 File: platform-kpi-dropdown.component.ts
 Description: Platform KPI dropdown menu component
 Purpose: Displays navigation menu for all platform KPI pages.
 Features: Toggle menu, navigate to platform pages, emit selections
*/

import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavOption } from '../../page-config';

/* ========== PLATFORM KPI DROPDOWN COMPONENT ========== */

@Component({
  selector: 'app-platform-kpi-dropdown',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './platform-kpi-dropdown.component.html',
  styleUrls: ['./platform-kpi-dropdown.component.scss']
})
export class PlatformKpiDropdownComponent {
  /* Input: Platform KPI navigation options */
  @Input() options: NavOption[] = [];
  /* Input: Menu open/closed state */
  @Input() isOpen: boolean = false;
  /* Output: Emit menu toggle request */
  @Output() toggleMenu = new EventEmitter<void>();
  /* Output: Emit menu close request */
  @Output() closeMenu = new EventEmitter<void>();
  /* Output: Emit selected route path */
  @Output() selection = new EventEmitter<string>();

  constructor(private router: Router) {}

  /* Toggle dropdown menu visibility */
  onToggleMenu(): void {
    this.toggleMenu.emit();
  }

  /* Navigate to selected page and close menu */
  onSelection(path: string): void {
    this.router.navigate([path]).then(() => {
      this.closeMenu.emit();
      this.selection.emit(path);
    }).catch((error) => {
      console.error('Navigation error:', error);
      this.closeMenu.emit();
    });
  }
}

