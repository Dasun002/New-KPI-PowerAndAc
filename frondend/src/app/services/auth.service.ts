/* File: auth.service.ts
   Description: Authentication service
   Purpose: Manages user authentication, token storage, and user state.
   Features: Azure login verification, JWT token management, user role checking,
   page permissions, logout functionality
*/

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { BehaviorSubject, Observable, map, tap } from 'rxjs'; // Import necessary RxJS operators
import { environment } from '../../environments/environment';

/* ========== DATA INTERFACES ========== */

/* Authenticated user object */
export interface User {
  /* JWT authentication token */
  token: string;
  /* User's display name */
  name: string;
  /* User's role (Admin, User, etc.) */
  role: string;
  /* Assigned pages for user */
  pages?: string[];
  /* Alternative field name for pages */
  assignedPages?: string[];
}

/* Login API response structure */
type LoginResponse = {
  token: string;
  Name?: string;
  Role?: string;
  Pages?: string[];
  AssignedPages?: string[];
  name?: string;
  role?: string;
  pages?: string[];
  assignedPages?: string[];
};

/* ========== AUTHENTICATION SERVICE ========== */

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  /* Backend API endpoint for authentication */
  private readonly apiUrl = `${environment.apiUrl}/auth`;
  /* Observable user state subject */
  private userSubject = new BehaviorSubject<User | null>(this.getUserFromStorage());
  /* Public observable for user state */
  public user$ = this.userSubject.asObservable();

  // Injects the required dependencies.
  constructor(private http: HttpClient, private router: Router) { }

  // Retrieves current user value from subject
  public get userValue(): User | null {
    return this.userSubject.value;
  }

  // Service-ID-only login removed - Azure authentication is now mandatory

  // Verifies Azure login and authenticates user with service ID
  verifyAzureLogin(email: string, serviceId: string): Observable<User> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/verify-azure-login`, { email, serviceId })
      .pipe(
        map(res => this.mapLoginResponse(res)),
        tap(user => this.handleAuthSuccess(user))
      );
  }

  devLogin(): Observable<User> {
    return this.http.post<LoginResponse>(`${this.apiUrl}/dev-login`, {})
      .pipe(
        map(res => this.mapLoginResponse(res)),
        tap(user => this.handleAuthSuccess(user))
      );
  }

 

  /* Map login response from multiple naming conventions to User object */
  // Maps login response.
  private mapLoginResponse(res: LoginResponse): User {
    return {
      token: res.token,
      name: res.name ?? res.Name ?? '',
      role: res.role ?? res.Role ?? '',
      pages: res.pages ?? res.Pages ?? [],
      assignedPages: res.assignedPages ?? res.AssignedPages ?? []
    };
  }

  /* Store user in localStorage and update subject */
  // Handles auth success.
  private handleAuthSuccess(user: User) {
    localStorage.setItem('user', JSON.stringify(user));
    this.userSubject.next(user);
  }

  /* Clear user session and redirect to login */
  // Handles user logout.
  logout() {
    localStorage.removeItem('user');
    this.userSubject.next(null);
    this.router.navigate(['/login']);
  }

  // Retrieves user from browser storage
  private getUserFromStorage(): User | null {
    const userStr = localStorage.getItem('user');
    return userStr ? JSON.parse(userStr) : null;
  }

  // Gets the current user's authentication token
  getToken(): string | null {
    return this.userValue?.token || null;
  }

  // Gets the current user's role
  getRole(): string | null {
    return this.userValue?.role || null;
  }

  // Gets the list of all allowed pages for the current user
  getAllowedPages(): string[] {
    return this.userValue?.pages || [];
  }

  // Gets the list of assigned pages for the current user
  getAssignedPages(): string[] {
    return this.userValue?.assignedPages || [];
  }

  // Checks if user can edit a specific page based on role and page assignment
  canEditPage(pageName: string): boolean {
    const normalize = (value: string | null | undefined) =>
      (value ?? '').trim().toLowerCase().replace(/[\s_-]+/g, '');

    const role = normalize(this.getRole());
    if (!role) {
      return false;
    }

    // Only PlatformAdmin can edit, and only for explicitly assigned pages
    if (role !== 'platformadmin') {
      return false;
    }

    const needle = normalize(pageName);
    if (!needle) {
      return false;
    }

    const assigned = this.getAssignedPages().map(normalize).filter(Boolean);
    if (assigned.includes(needle)) {
      return true;
    }

    // Fall back to allowed page list when no explicit assignment
    const allowed = this.getAllowedPages().map(normalize).filter(Boolean);
    return allowed.includes(needle);
  }
}
