/*
 File: logout-button.component.ts
 Description: Logout button component
 Purpose: Provides logout functionality trigger for parent component.
*/

import { Component, Output, EventEmitter } from '@angular/core';

/* ========== LOGOUT BUTTON COMPONENT ========== */

@Component({
  selector: 'app-logout-button',
  standalone: true,
  imports: [],
  templateUrl: './logout-button.component.html',
  styleUrls: ['./logout-button.component.scss']
})
export class LogoutButtonComponent {
  /* Output: Emit logout request */
  @Output() logout = new EventEmitter<void>();

  /* Emit logout event */
  onLogout(): void {
    this.logout.emit();
  }
}

