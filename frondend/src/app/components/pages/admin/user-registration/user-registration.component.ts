/*
 File: user-registration.component.ts
 Description: User account registration and management page
 Purpose: Allows admins to create, update, and delete standard user accounts.
 Features: Form validation, user listing, role assignment, page access control
*/

// src/app/components/pages/admin/user-registration/user-registration.component.ts
import { CommonModule } from '@angular/common';
import { HttpClientModule } from '@angular/common/http';
import { ChangeDetectorRef, Component, ElementRef, inject, OnInit, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { environment } from '../../../../../environments/environment';
import { CreateUserDto, UpdateUserDto, User, UserService } from '../../../../services/user.service';

/* ========== USER REGISTRATION COMPONENT ========== */

interface PageOption {
  pageId: number;
  name: string;
}

@Component({
  selector: 'app-user-registration',
  standalone: true,
  imports: [CommonModule, FormsModule, HttpClientModule],
  templateUrl: './user-registration.component.html',
  styleUrls: ['./user-registration.component.scss']
})
export class UserRegistrationComponent implements OnInit {
  private readonly cdr = inject(ChangeDetectorRef);
  pageTitle = 'User Management';
  @ViewChild('formCard') formCard?: ElementRef<HTMLElement>;
  @ViewChild('nameField') nameField?: ElementRef<HTMLInputElement>;

  formData: CreateUserDto = {
    serviceId: '',
    name: '',
    pageIds: [],
    pages: [],
    role: 'User', // Default to User
    isActive: true
  };

  users: User[] = [];
  error = '';
  success = '';
  editingUser: User | null = null;
  isLoading = false;

  availablePages: PageOption[] = [
    { pageId: 1, name: 'IP NW OP' },
    { pageId: 2, name: 'Service Fulfilment' },
    { pageId: 3, name: 'Wireline Access NW' },
    { pageId: 4, name: 'OTN OP' },
    { pageId: 6, name: 'Routine Maintenance' },
    { pageId: 7, name: 'Tower Maintainance' },
    { pageId: 8, name: 'Enterprise KPI' },
    { pageId: 9, name: 'Other Operator KPI' },
    { pageId: 10, name: 'Other KPI' }
  ];

  // Injects the required dependencies.
  constructor(private userService: UserService) { }

  // Loads the initial component state.
  ngOnInit() {
    // Load the user list before allowing create, edit, or delete actions.
    this.fetchUsers();
  }

  // Fetches users.
  fetchUsers() {
    // Retrieve users and keep the page-level loading/error state synchronized with the API.
    this.isLoading = true;
    this.userService.getAllUsers().subscribe({
      next: (users: User[]) => {
        this.users = users;
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: (error: any) => {
        this.error = 'Failed to fetch users. Please check if backend is running.';
        console.error('Error fetching users:', error);
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  handlePageChange(page: PageOption) {
    // Platform admins may receive one page; other roles can toggle multiple page assignments.
    // For PlatformAdmin enforce single selection; for others allow multi
    if (this.formData.role === 'PlatformAdmin') {
      this.formData.pageIds = [page.pageId];
    } else {
      const index = this.formData.pageIds.indexOf(page.pageId);
      if (index > -1) {
        this.formData.pageIds.splice(index, 1);
      } else {
        this.formData.pageIds.push(page.pageId);
      }
    }
    this.syncSelectedPageNames();
  }

  handleRoleChange() {
    // Enforce the same single-page rule if the role changes after pages were selected.
    if (this.formData.role === 'PlatformAdmin' && this.formData.pageIds.length > 1) {
      this.formData.pageIds = [this.formData.pageIds[0]];
      this.syncSelectedPageNames();
    }
  }

  isPageSelected(pageId: number): boolean {
    return this.formData.pageIds.includes(pageId);
  }

  // Handles submit.
  handleSubmit(event: Event) {
    // Validate identity fields, then create a new user or update the selected existing user.
    event.preventDefault();
    this.error = '';
    this.success = '';

    if (!this.formData.name.trim()) {
      this.error = 'Name is required';
      return;
    }

    if (!this.formData.serviceId.trim()) {
      this.error = 'Service ID is required';
      return;
    }

    this.isLoading = true;

    if (this.editingUser) {
      const updateData: UpdateUserDto = {
        serviceId: this.formData.serviceId,
        name: this.formData.name,
        role: this.formData.role,
        isActive: this.formData.isActive,
        pageIds: this.formData.pageIds,
        pages: this.formData.pages
      };

      // Ensure we use userId (number) for update
      this.userService.updateUser(this.editingUser.userId.toString(), updateData).subscribe({
        next: () => {
          this.success = 'User updated successfully';
          this.fetchUsers();
          this.resetForm();
          this.isLoading = false;
          this.cdr.detectChanges();
        },
        error: (error: any) => {
          this.error = 'Failed to update user: ' + error.message;
          console.error('Update error:', error);
          this.isLoading = false;
          this.cdr.detectChanges();
        }
      });
    } else {
      const createData: CreateUserDto = {
        ...this.formData,
        pageIds: this.formData.pageIds,
        pages: this.formData.pages
      };

      this.userService.createUser(createData).subscribe({
        next: (newUser: User) => {
          this.success = 'User created successfully';
          this.users.push(newUser);
          this.resetForm();
          this.isLoading = false;
          this.cdr.detectChanges();
        },
        error: (error: any) => {
          this.error = 'Failed to create user: ' + error.message;
          console.error('Create error:', error);
          this.isLoading = false;
          this.cdr.detectChanges();
        }
      });
    }
  }

  // Handles edit.
  handleEdit(user: User) {
    // Convert saved page names/IDs into the form model and focus the edit form.
    const pageIds = user.pageIds?.length ? [...user.pageIds] : this.getPageIdsFromNames(user.pages);

    this.editingUser = user;
    this.formData = {
      serviceId: user.serviceId,
      name: user.name,
      pageIds,
      pages: this.getPageNamesFromIds(pageIds),
      role: user.role,
      isActive: user.isActive
    };

    // Enforce single selection UI for PlatformAdmin
    if (this.formData.role === 'PlatformAdmin' && this.formData.pageIds.length > 1) {
      this.formData.pageIds = [this.formData.pageIds[0]];
      this.syncSelectedPageNames();
    }

    setTimeout(() => {
      this.formCard?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
      this.nameField?.nativeElement.focus();
    }, 50);
  }

  handleDelete(id: number) {
    // Confirm deletion, remove the user locally, and cancel any edit for that user.
    if (!confirm('Are you sure you want to delete this user?')) return;

    this.isLoading = true;
    this.userService.deleteUser(id.toString()).subscribe({
      next: () => {
        this.success = 'User deleted successfully';
        this.users = this.users.filter(u => u.userId !== id);
        if (this.editingUser && this.editingUser.userId === id) {
          this.cancelEdit();
        }
        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: (error: any) => {
        this.error = 'Failed to delete user: ' + error.message;
        console.error('Delete error:', error);
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  // Cancels the current edit.
  cancelEdit() {
    // Leave edit mode and restore the default new-user form state.
    this.editingUser = null;
    this.resetForm();
  }

  // Resets form.
  private resetForm() {
    // Reset identity, access, role, and active-status fields to safe defaults.
    this.formData = {
      serviceId: '',
      name: '',
      pageIds: [],
      pages: [],
      role: 'User',
      isActive: true
    };
    this.editingUser = null;
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

  // Fills basic test data.
  fillBasicTestData() {
    // Populate minimal local test values without sending a request to the backend.
    this.formData.name = 'Test User';
    this.formData.serviceId = (10000 + this.users.length).toString();
    this.formData.pageIds = this.availablePages.length ? [this.availablePages[0].pageId] : [];
    this.syncSelectedPageNames();
  }

  // Selects all pages.
  selectAllPages() {
    // Select every available page except that PlatformAdmin remains limited to one page.
    this.formData.pageIds = this.formData.role === 'PlatformAdmin'
      ? (this.availablePages.length ? [this.availablePages[0].pageId] : [])
      : this.availablePages.map(page => page.pageId);
    this.syncSelectedPageNames();
  }

  // Clears pages.
  clearPages() {
    // Remove all page assignments and keep the derived page-name list in sync.
    this.formData.pageIds = [];
    this.syncSelectedPageNames();
  }

  // Syncs selected page names.
  private syncSelectedPageNames() {
    // Keep the human-readable page names aligned with the selected page IDs.
    this.formData.pages = this.getPageNamesFromIds(this.formData.pageIds);
  }

  private getPageNamesFromIds(pageIds: number[]): string[] {
    // Convert persisted page IDs into names used by the user DTO and template.
    return pageIds
      .map(pageId => this.availablePages.find(page => page.pageId === pageId)?.name)
      .filter((name): name is string => !!name);
  }

  private getPageIdsFromNames(pageNames: string[] = []): number[] {
    // Recover IDs from legacy name-based assignments using normalized comparisons.
    return pageNames
      .map(pageName => this.availablePages.find(page => this.normalizePageName(page.name) === this.normalizePageName(pageName))?.pageId)
      .filter((pageId): pageId is number => pageId !== undefined);
  }

  private normalizePageName(pageName: string): string {
    // Ignore spaces, punctuation, and casing when matching page names from the API.
    return pageName.toLowerCase().replace(/[^a-z0-9]/g, '');
  }

  // Handles test backend connection.
  testBackendConnection() {
    // Perform a lightweight connectivity check against the users endpoint for troubleshooting.
    this.error = '';
    this.success = 'Testing backend connection...';
    fetch(`${environment.apiUrl}/users`)
      .then(response => {
        if (response.ok) {
          this.success = 'Backend connection successful!';
        } else {
          this.error = `Backend returned status: ${response.status}`;
        }
      })
      .catch(err => {
        this.error = 'Cannot connect to backend. Make sure the local API is running.';
        console.error('Connection test failed:', err);
      });
  }
}
