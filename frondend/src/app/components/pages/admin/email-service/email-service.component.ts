/*
 File: email-service.component.ts
 Description: Email recipient management page
 Purpose: Manages email distribution list for KPI notifications and reports.
 Features: Add/remove email recipients, recipient validation, list management
*/

import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { finalize } from 'rxjs/operators';
import { environment } from '../../../../../environments/environment';

/* ========== EMAIL SERVICE COMPONENT ========== */

/* Email recipient entity */
type EmailRecipient = {
  id: number;     // SQL id column (int identity)
  email: string;  // SQL email column
  v?: number;     // optional
};

@Component({
  selector: 'app-email-service',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, HttpClientModule],
  templateUrl: './email-service.component.html',
  styleUrls: ['./email-service.component.scss']
})
export class EmailServiceComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  private readonly cdr = inject(ChangeDetectorRef);

  pageTitle = 'Email Recipients Management';
  formTitle = 'Add Recipient';
  submitButtonLabel = 'Add Recipient';

  recipients: EmailRecipient[] = [];
  editingId: number | null = null;

  loading = false;
  saving = false;
  errorMessage = '';

  // ✅ IMPORTANT: use your backend base url
 // private readonly apiBase = 'http://localhost:5043/api/emails';
  // Use the environment API base so recipient management follows the active deployment.
  private readonly apiBase = `${environment.apiUrl}/emails`;

  form = this.fb.group({
    email: ['', [Validators.required, Validators.email]]
  });

  ngOnInit(): void {
    // Populate the recipient list when the page is first rendered.
    this.fetchData();
  }

  fetchData(): void {
    // Load recipients, sort them for stable display, and always clear the loading state.
    this.loading = true;
    this.errorMessage = '';

    this.http
      .get<EmailRecipient[]>(`${this.apiBase}/recipients`)
      .pipe(finalize(() => {
        this.loading = false;
        this.cdr.detectChanges();
      }))
      .subscribe({
        next: (response) => {
          // ✅ sort like your KPI tables
          this.recipients = (response ?? []).sort((a, b) =>
            (a.email ?? '').localeCompare(b.email ?? '')
          );
        },
        error: (err) => {
          console.error('Failed to fetch recipients', err);
          this.errorMessage = 'Unable to load email recipients from database.';
          this.recipients = [];
        }
      });
  }

  onSubmit(): void {
    // Validate the form, then choose add or update from the presence of editingId.
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const payload = {
      email: (this.form.value.email ?? '').trim()
    };

    if (!payload.email) {
      this.errorMessage = 'Email is required.';
      return;
    }

    let request$;
    if (this.editingId) {
      // Prevent update if editingId is invalid (0 or negative)
      if (this.editingId <= 0) {
        this.errorMessage = 'Invalid recipient for update.';
        return;
      }
      request$ = this.http.put(`${this.apiBase}/update-recipient/${this.editingId}`, payload);
    } else {
      request$ = this.http.post(`${this.apiBase}/add-recipient`, payload);
    }

    this.saving = true;
    this.errorMessage = '';

    request$
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: () => {
          this.resetForm();
          this.fetchData();
        },
        error: (err) => {
          console.error('Save failed', err);

          // ✅ if backend returns validation msg, show it
          const msg =
            err?.error?.message ||
            err?.error ||
            'Saving failed. Please try again.';

          this.errorMessage = String(msg);
        }
      });
  }

  onEdit(recipient: EmailRecipient): void {
    // Populate the form and switch labels so the next submit updates this recipient.
    this.editingId = recipient.id; // ✅ SQL id
    this.form.patchValue({ email: recipient.email });
    this.formTitle = 'Update Recipient';
    this.submitButtonLabel = 'Update Recipient';
    this.errorMessage = '';
  }

  onDelete(id: number): void {
    // Confirm deletion, remove the record on success, and reload the list.
    if (!id || id <= 0) {
      this.errorMessage = 'Invalid recipient for deletion.';
      return;
    }
    if (!window.confirm('Are you sure you want to delete this recipient?')) return;

    this.saving = true;
    this.errorMessage = '';

    this.http
      .delete(`${this.apiBase}/delete-recipient/${id}`)
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: () => {
          // If deleting the currently edited recipient, reset form
          if (this.editingId === id) {
            this.resetForm();
          }
          this.fetchData();
        },
        error: (err) => {
          console.error('Delete failed', err);
          this.errorMessage = 'Deletion failed. Please try again.';
        }
      });
  }

  onCancelEdit(): void {
    this.resetForm();
    this.formTitle = 'Add Recipient';
    this.submitButtonLabel = 'Add Recipient';
    this.errorMessage = '';
  }

  private resetForm(): void {
    // Return the form and button labels to the default add-recipient state.
    this.form.reset({ email: '' });
    this.editingId = null;
    this.formTitle = 'Add Recipient';
    this.submitButtonLabel = 'Add Recipient';
  }
}
