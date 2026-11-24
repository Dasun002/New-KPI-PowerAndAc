/* File: app.ts
   Description: Root Angular application component
   Purpose: Main app shell component that provides navigation structure,
   header, dropdown menus, and routing outlet for all pages.
   Features: Navigation state management, router event handling, user authentication status
*/

import { Component, HostListener, signal, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterOutlet, Router, NavigationEnd, NavigationError, Event as RouterEvent } from '@angular/router';
// import { MsalService } from '@azure/msal-angular'; // Removed
import { adminNavOptions, overallNavOptions, platformNavOptions } from './page-config';
import { HeaderTitleComponent } from './components/header-title/header-title.component';
import { DashboardButtonComponent } from './components/dashboard-button/dashboard-button.component';
import { Q1ButtonComponent } from './components/2026-q1-button/2026-q1-button.component';
import { AnalyticsButtonComponent } from './components/analytics-button/analytics-button.component';
import { OverallKpiDropdownComponent } from './components/overall-kpi-dropdown/overall-kpi-dropdown.component';
import { PlatformKpiDropdownComponent } from './components/platform-kpi-dropdown/platform-kpi-dropdown.component';
import { AdminDropdownComponent } from './components/admin-dropdown/admin-dropdown.component';
import { LogoutButtonComponent } from './components/logout-button/logout-button.component';
import { HasRoleDirective } from './directives/has-role.directive';
import { AuthService } from './services/auth.service';

/* ========== ROOT COMPONENT ========== */

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    CommonModule,
    RouterOutlet,
    HeaderTitleComponent,
    DashboardButtonComponent,
    Q1ButtonComponent,
    AnalyticsButtonComponent,
    OverallKpiDropdownComponent,
    PlatformKpiDropdownComponent,
    AdminDropdownComponent,
    LogoutButtonComponent,
    HasRoleDirective
  ],
  templateUrl: './app.html',
  styleUrls: ['./app.css']
})
export class App implements OnInit {
  /* Application title */
  protected readonly title = signal('Network KPI Portal');
  /* Currently open dropdown menu */
  protected readonly openMenu = signal<string | null>(null);
  /* Logged-in user name */
  protected readonly userName = signal<string>('Guest');
  /* Logged-in user role */
  protected readonly userRole = signal<string>('');
  /* Navigation options for Overall KPI section */
  protected readonly overallOptions = overallNavOptions;
  /* Navigation options for Platform KPI section */
  protected readonly platformOptions = platformNavOptions;
  /* Navigation options for Admin section */
  protected readonly adminOptions = adminNavOptions;
  /* Current URL path */
  protected currentUrl = '';
  /* Navigation error message */
  protected navError: string | null = null;
  /* Last error encountered */
  protected lastError: string | null = null;

  // Injects the required dependencies.
  constructor(private authService: AuthService, private router: Router) { }

  /* Initialize component and set up router event subscriptions */
  ngOnInit(): void {
    this.currentUrl = this.router.url;
    this.router.events.subscribe((ev: RouterEvent) => {
      if (ev instanceof NavigationEnd) {
        this.currentUrl = ev.urlAfterRedirects;
        this.navError = null;
      }
      if (ev instanceof NavigationError) {
        this.navError = String(ev.error || 'NavigationError');
      }
    });

    /* Subscribe to user authentication state changes */
    this.authService.user$.subscribe(user => {
      if (user) {
        this.userName.set(user.name);
        this.userRole.set(user.role);
      } else {
        this.userName.set('Guest');
        this.userRole.set('');
      }
    });
  }

  /* Handle menu selection (delegated to dropdown components) */
  protected handleSelection(path: string): void {
    // Navigation is handled by the dropdown components
  }

  /* Execute user logout and clear session */
  protected logout(): void {
    console.log('[Navigation] Logout requested');
    this.authService.logout();
    this.closeMenus();
  }

  protected get isLoginPage(): boolean {
    const path = window.location.pathname;
    return path.startsWith('/login') || path === '/';
  }

  protected toggleMenu(menu: string): void {
    this.openMenu.update((current) => (current === menu ? null : menu));
  }

  protected closeMenus(): void {
    this.openMenu.set(null);
  }

  protected navigateToDashboard(): void {
    this.router.navigate(['/dashboard']);
  }

  protected navigateToUserManagement(): void {
    const role = this.authService.getRole();
    if (role === 'Admin' || role === 'SuperAdmin') {
      this.router.navigate(['/admin/user-registration']);
    }
  }

  @HostListener('document:click', ['$event'])
  protected handleDocumentClick(event: Event): void {
    const target = event.target as HTMLElement | null;
    if (target && target.closest('.dropdown')) {
      return;
    }
    this.closeMenus();
  }
}
