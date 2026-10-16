/*
 File: tm-activity-plan.component.ts
 Description: TM Activity Plan KPI admin management
 Purpose: CRUD operations for TM activity plan KPI metrics.
 Features: Data table management, activity tracking, form validation
*/

import { CommonModule } from '@angular/common';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { environment } from '../../../../../environments/environment';

/* ========== TM ACTIVITY PLAN ADMIN COMPONENT ========== */

/* Type definition for activity record */
type ActivityRecord = {
  id?: number; // Backend uses 'id' (int)
  kpi: string;
  target: string;
  calculation: string;
  platform: string;
  responsibleDGM: string;
  definedOLADetails: string;
  dataSources: string;
};

@Component({
selector: 'app-tower-mtnc',

  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, HttpClientModule],
  templateUrl: './tower-mtnc.component.html',
  styleUrls: ['./tower-mtnc.component.scss']

})
export class TowerMtncComponent implements OnInit {

  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  private readonly cdr = inject(ChangeDetectorRef);

  pageTitle = 'Tower Maintainance';
  records: ActivityRecord[] = [];
  editingId: number | null = null;
  loading = false;
  saving = false;
  errorMessage = '';

  form = this.fb.group({
    kpi: ['', Validators.required],
    target: ['', Validators.required],
    calculation: ['', Validators.required],
    platform: ['', Validators.required],
    responsibleDGM: [''],
    definedOLADetails: [''],
    dataSources: ['']
  });

  ngOnInit(): void {
    // Load tower activity-plan definitions when the admin page opens.
    this.fetchData();
  }

  fetchData(): void {
    // Read records and expose a consistent loading/error state to the template.
    this.loading = true;
    this.errorMessage = '';
    this.http
      .get<ActivityRecord[]>(`${environment.apiUrl}/TmActivityPlans`)
      .pipe(finalize(() => {
        this.loading = false;
        this.cdr.detectChanges();
      }))
      .subscribe({
        next: response => {
          this.records = response || [];
        },
        error: err => {
          console.error('Failed to fetch data', err);
          this.errorMessage = 'Unable to load Tower Maintenance activities.';
        }
      });
  }

  onSubmit(): void {
    // Validate the form, normalize editable fields, and choose add or update.
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const rawForm = this.form.getRawValue();
    const payload = {
      kpi: (rawForm.kpi ?? '').trim(),
      target: (rawForm.target ?? '').trim(),
      calculation: (rawForm.calculation ?? '').trim(),
      platform: (rawForm.platform ?? '').trim(),
      responsibleDGM: '-',
      definedOLADetails: '-',
      dataSources: '-'
    };
    const request$ = this.editingId
      ? this.http.put(`${environment.apiUrl}/TmActivityPlans/${this.editingId}`, { id: this.editingId, ...payload })
      : this.http.post(`${environment.apiUrl}/TmActivityPlans`, payload);

    this.saving = true;
    request$
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: () => {
          this.resetForm();
          this.fetchData();
        },
        error: err => {
          console.error('Failed to save data', err);
          this.errorMessage = 'Saving failed. Please try again.';
        }
      });
  }

  onEdit(record: ActivityRecord): void {
    // Copy a selected activity into the form and retain its ID for update requests.
    this.editingId = record.id!;
    this.form.patchValue({
      kpi: record.kpi,
      target: record.target,
      calculation: record.calculation,
      platform: record.platform,
      responsibleDGM: record.responsibleDGM,
      definedOLADetails: record.definedOLADetails,
      dataSources: record.dataSources
    });
  }

  onDelete(id: number): void {
    // Confirm deletion before removing the selected activity-plan record.
    if (!window.confirm('Are you sure you want to delete this item?')) {
      return;
    }

    this.saving = true;
    this.http
      .delete(`${environment.apiUrl}/TmActivityPlans/${id}`)
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: () => this.fetchData(),
        error: err => {
          console.error('Failed to delete data', err);
          this.errorMessage = 'Deletion failed. Please try again.';
        }
      });
  }

  onCancelEdit(): void {
    // Discard unsaved edits and return the form to add mode.
    this.resetForm();
  }

  private resetForm(): void {
    // Clear controls and the edit identifier after save, cancel, or delete.
    this.form.reset({
      kpi: '',
      target: '',
      calculation: '',
      platform: '',
      responsibleDGM: '',
      definedOLADetails: '',
      dataSources: ''
    });
    this.editingId = null;
  }
}

