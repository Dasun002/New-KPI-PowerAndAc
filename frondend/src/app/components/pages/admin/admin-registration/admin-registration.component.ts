/*
 File: admin-registration.component.ts
 Description: Admin user registration and management page
 Purpose: Allows Super Admin to create, update, and delete admin user accounts.
 Features: Form validation, user listing, edit functionality, page/role assignment
*/

import { CommonModule } from '@angular/common';
import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpClientModule, HttpErrorResponse } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';

/* ========== DATA INTERFACES ========== */

/* Admin user entity */
interface AdminUser {
  userId: number;       // Matches Backend UserDto.UserId
  name: string;
  serviceId: string;    // Matches Backend UserDto.ServiceId
  role?: string;
  isActive: boolean;
  createdAt: string;
  lastLogin?: string;
  pages?: string[];
}

interface CreateAdminRequest {
  name: string;
  serviceId: string;
  role?: string;
  pages?: string[];
  isActive: boolean;
}

@Component({
  selector: 'app-admin-registration',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, HttpClientModule],
  templateUrl: './admin-registration.component.html',
  styleUrls: ['./admin-registration.component.scss'],
})
export class AdminRegistrationComponent implements OnInit {
  pageTitle = 'Network KPI Monitoring';
  sectionTitle = 'Admin Management';

  adminForm!: FormGroup;

  admins: AdminUser[] = [];
  filteredAdmins: AdminUser[] = [];

  successMessage = '';
  errorMessage = '';
  searchTerm = '';
  isSubmitting = false;
  isFormVisible = false;
  statusFilter: 'all' | 'active' | 'inactive' = 'all';

  // ✅ Change this if your backend port changes
  //private readonly apiBase = 'http://localhost:5043/api/users';
  // Use the environment API base so development and production share the same component logic.
  private readonly apiBase = `${environment.apiUrl}/users`;

  eligibleUsers: AdminUser[] = [];
  selectedUserId: number | null = null;

  // Injects the required dependencies.
  constructor(
    private fb: FormBuilder,
    private http: HttpClient,
    private cdr: ChangeDetectorRef
  ) { }

  ngOnInit(): void {
    // Build the selector form and load both current admins and users eligible for promotion.
    this.buildForm();
    this.loadAdminsFromApi();
    this.loadEligibleUsers();
  }

  private buildForm(): void {
    // The form stores the selected existing user; name and service ID come from the user record.
    this.adminForm = this.fb.group({
      userId: ['', Validators.required] // Select user ID instead of typing name/serviceId
    });
  }

  // Load ALL users to find eligible ones (User/PlatformAdmin)
  loadEligibleUsers(): void {
    // Exclude users who already have Admin or SuperAdmin privileges from the promotion selector.
    this.http.get<AdminUser[]>(`${this.apiBase}`).subscribe({ // GET /api/users
      next: (data) => {
        // Filter out SuperAdmins and Admins (already in the list)
        this.eligibleUsers = (data ?? []).filter(u => u.role !== 'SuperAdmin' && u.role !== 'Admin');
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Failed to load eligible users', err);
        this.cdr.detectChanges();
      }
    });
  }

  //  Load from backend
  loadAdminsFromApi(): void {
    // Load the current admin list before applying the local search/status filters.
    this.errorMessage = '';
    this.http.get<AdminUser[]>(`${this.apiBase}/admins`).subscribe({
      next: (data) => {
        this.admins = data ?? [];
        this.applyFilters();
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.errorMessage = this.getApiError(err, 'Failed to load admins.');
        this.cdr.detectChanges();
      },
    });
  }

  toggleFormVisibility(): void {
    // Refresh eligible users whenever the promotion form is opened.
    this.isFormVisible = !this.isFormVisible;
    if (this.isFormVisible) {
      this.loadEligibleUsers(); // Refresh list when opening form
    }
  }

  get userIdCtrl() {
    return this.adminForm.get('userId');
  }

  getActiveCount(): number {
    return this.admins.filter((a) => a.isActive).length;
  }

  getInactiveCount(): number {
    return this.admins.filter((a) => !a.isActive).length;
  }

  // PROMOTE USER TO ADMIN (PATCH)
  onSubmit(): void {
    // Promote the selected eligible user and update the local lists after the API succeeds.
    if (this.adminForm.invalid || this.isSubmitting) return;

    this.errorMessage = '';
    this.successMessage = '';
    this.isSubmitting = true;

    const userId = Number(this.adminForm.get('userId')?.value);
    const userToPromote = this.eligibleUsers.find(u => u.userId === userId);

    if (!userToPromote) {
      this.errorMessage = 'Selected user not found.';
      this.isSubmitting = false;
      return;
    }

    this.http.patch(`${this.apiBase}/${userId}/promote`, {}).subscribe({
      next: () => {
        // Update local arrays
        userToPromote.role = 'Admin';
        this.admins = [userToPromote, ...this.admins];
        this.eligibleUsers = this.eligibleUsers.filter(u => u.userId !== userId);
        this.applyFilters();

        this.successMessage = `User "${userToPromote.name}" promoted to Admin successfully.`;
        this.adminForm.reset();
        this.isSubmitting = false;
        this.cdr.detectChanges();

        setTimeout(() => (this.successMessage = ''), 3000);
      },
      error: (err) => {
        this.errorMessage = this.getApiError(err, 'Failed to promote user.');
        this.isSubmitting = false;
        this.cdr.detectChanges();
      },
    });
  }

  // ✅ DEMOTE ADMIN (PATCH)
  deleteAdmin(id: number): void {
    // Demote an administrator through the API, then remove the account from the admin table.
    const admin = this.admins.find((a) => a.userId === id);
    if (!admin) return;

    if (!window.confirm(`Remove admin privileges from "${admin.name}" (${admin.serviceId})? They will become a regular User.`)) return;

    this.errorMessage = '';
    this.successMessage = '';

    this.http.patch(`${this.apiBase}/${id}/demote`, {}).subscribe({
      next: () => {
        this.admins = this.admins.filter((a) => a.userId !== id);
        this.applyFilters();

        this.successMessage = `Admin "${admin.name}" removed from admin list (demoted to User).`;
        this.cdr.detectChanges();
        setTimeout(() => (this.successMessage = ''), 3000);
      },
      error: (err) => {
        this.errorMessage = this.getApiError(err, 'Failed to remove admin.');
        this.cdr.detectChanges();
      },
    });
  }

  // ✅ TOGGLE ACTIVE/INACTIVE (PATCH)
  toggleStatus(admin: AdminUser): void {
    // Toggle status immediately for responsive UI, rolling back if the server rejects the change.
    this.errorMessage = '';
    this.successMessage = '';

    // optimistic update (instant UI)
    const oldValue = admin.isActive;
    admin.isActive = !admin.isActive;
    this.applyFilters();

    this.http.patch(`${this.apiBase}/${admin.userId}/status`, {}).subscribe({
      next: () => {
        this.successMessage = `Admin "${admin.name}" ${admin.isActive ? 'activated' : 'deactivated'}.`;
        this.cdr.detectChanges();
        setTimeout(() => (this.successMessage = ''), 2000);
      },
      error: (err) => {
        // rollback if api fails
        admin.isActive = oldValue;
        this.applyFilters();
        this.errorMessage = this.getApiError(err, 'Failed to update status.');
        this.cdr.detectChanges();
      },
    });
  }

  filterAdmins(): void {
    this.applyFilters();
  }

  setStatusFilter(filter: 'all' | 'active' | 'inactive'): void {
    if (this.statusFilter === filter) return;
    this.statusFilter = filter;
    this.applyFilters();
  }

  private applyFilters(): void {
    // Apply status first and then case-insensitive name/service-ID search to the rendered list.
    const normalizedSearch = this.searchTerm.trim().toLowerCase();

    let data = [...this.admins];

    if (this.statusFilter === 'active') {
      data = data.filter((admin) => admin.isActive);
    } else if (this.statusFilter === 'inactive') {
      data = data.filter((admin) => !admin.isActive);
    }

    if (normalizedSearch) {
      data = data.filter(
        (admin) =>
          admin.name?.toLowerCase().includes(normalizedSearch) ||
          admin.serviceId?.includes(normalizedSearch)
      );
    }

    this.filteredAdmins = data;
  }

  getAvatarColor(name: string): string {
    const colors = [
      'linear-gradient(135deg, #0057a6, #0077cc)',
      'linear-gradient(135deg, #00a86b, #00cc88)',
      'linear-gradient(135deg, #8e44ad, #9b59b6)',
      'linear-gradient(135deg, #ff6b35, #ff9e5c)',
      'linear-gradient(135deg, #3498db, #2ecc71)',
    ];
    const safe = name || 'A';
    const index = safe.charCodeAt(0) % colors.length;
    return colors[index];
  }

  formatDate(dateString?: string): string {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return isNaN(date.getTime())
      ? dateString
      : date.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
  }

  formatTime(dateString?: string): string {
    if (!dateString) return '-';
    const date = new Date(dateString);
    return isNaN(date.getTime())
      ? dateString
      : date.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit', hour12: true });
  }

  trackByAdmin(_: number, admin: AdminUser): number {
    return admin.userId;
  }

  private getApiError(err: any, fallback: string): string {
    // Backend may return plain text (BadRequest/Conflict) or JSON
    if (err instanceof HttpErrorResponse) {
      if (typeof err.error === 'string' && err.error.trim()) return err.error;
      if (err.error?.message) return err.error.message;
      if (err.status === 0) return 'Backend not reachable. Is API running on http://localhost:5043 ?';
      return `${fallback} (HTTP ${err.status})`;
    }
    return fallback;
  }
}
