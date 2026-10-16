/*
 File: admin-dropdown.component.ts
 Description: Admin dropdown menu component
 Purpose: Displays categorized navigation menu for admin and platform pages.
 Features: Groups options by admin/platform/other themes, handles navigation
*/

import { Component, Input, Output, EventEmitter, OnChanges, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { NavOption } from '../../page-config';
import { AuthService } from '../../services/auth.service';

/* ========== DATA INTERFACES ========== */

/* Navigation menu section grouping */
interface DropdownSection {
  /* Section display title */
  title: string;
  /* Visual theme for section styling */
  theme: 'admin' | 'platform' | 'other';
  /* Navigation options in this section */
  options: NavOption[];
}

/* ========== ADMIN DROPDOWN COMPONENT ========== */

@Component({
  selector: 'app-admin-dropdown',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './admin-dropdown.component.html',
  styleUrls: ['./admin-dropdown.component.scss']
})
export class AdminDropdownComponent implements OnChanges {
  /* Input: Navigation options to display */
  @Input() options: NavOption[] = [];
  /* Input: Whether dropdown menu is open */
  @Input() isOpen: boolean = false;
  /* Output: Emit when menu toggle requested */
  @Output() toggleMenu = new EventEmitter<void>();
  /* Output: Emit when menu close requested */
  @Output() closeMenu = new EventEmitter<void>();
  /* Output: Emit selected route path */
  @Output() selection = new EventEmitter<string>();

  /* Grouped navigation sections */
  sections: DropdownSection[] = [];

  /* Labels for admin-related navigation options */
  private readonly adminSet = new Set([
    'Admin Management',
    'User Management',
    'Region Management',
    'E-mail Service',
    'KPI Management'
  ]);

  /* Labels for platform-related navigation options */
  private readonly platformSet = new Set([
    'Service Fulfilment',
    'Wireline Access NW',

    'IP NW OP',
    'OTN OP 1',
    'OTN OP 2',
    'Enterprise KPI',
    'Other KPI'
  ]);

  private readonly authService = inject(AuthService);

  // Injects the required dependencies.
  constructor(private router: Router) {}

  /* Rebuild sections when options input changes */
  ngOnChanges(changes: SimpleChanges): void {
    if (changes['options']) {
      this.buildSections();
    }
  }

  /* Group navigation options into themed sections */
  private buildSections(): void {
    const role = this.authService.getRole();
    const admin: NavOption[] = [];
    const platform: NavOption[] = [];
    const others: NavOption[] = [];

    for (const option of this.options) {
      // If role is PlatformAdmin or User, only expose KPI Management
      if ((role === 'PlatformAdmin' || role === 'User') && option.label !== 'KPI Management') {
        continue;
      }

      if (this.adminSet.has(option.label)) {
        admin.push(option);
      } else if (this.platformSet.has(option.label)) {
        platform.push(option);
      } else {
        others.push(option);
      }
    }

    const sections: DropdownSection[] = [
      { title: 'Admin / User / Email / KPI Management', theme: 'admin', options: admin },
      { title: 'Platform Modules', theme: 'platform', options: platform },
      { title: 'Operations & Other Modules', theme: 'other', options: others }
    ];

    this.sections = sections.filter(section => section.options.length > 0);
  }

  /* Toggle dropdown menu visibility */
  onToggleMenu(): void {
    const role = this.authService.getRole();
    if (role === 'PlatformAdmin' || role === 'User') {
      this.router.navigate(['admin/final-table']).then(() => {
        this.closeMenu.emit();
      });
    } else {
      this.toggleMenu.emit();
    }
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

  get dropdownLabel(): string {
    const role = this.authService.getRole();
    if (role === 'PlatformAdmin' || role === 'User') {
      return 'KPI Management';
    }
    return 'Admin';
  }

  get isDropdown(): boolean {
    const role = this.authService.getRole();
    return role !== 'PlatformAdmin' && role !== 'User';
  }
}

