/*
 File: routine-mtnc.component.ts
 Description: Routine maintenance KPI admin management
 Purpose: CRUD operations for routine maintenance KPI definitions.
 Features: Data table management, add/edit/delete operations, form validation
*/

import { CommonModule } from '@angular/common';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { environment } from '../../../../../environments/environment';

/* ========== DATA TYPES ========== */

/* Routine maintenance record */
type RoutineRecord = {
  _id: number;  // Changed from string to number (int identity)
  kpi: string;
  target: string;
  calculation: string;
  platform: string;
  responsibleDGM: string;
  definedOLADetails: string;
  dataSources: string;
};

@Component({
  selector: 'app-admin-routine-mtnc',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, HttpClientModule],
  templateUrl: './routine-mtnc.component.html',
  styleUrls: ['./routine-mtnc.component.scss']
})
export class AdminRoutineMtncComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  private readonly cdr = inject(ChangeDetectorRef);

  pageTitle = 'Routine Maintenance';
  formTitle = 'Add KPI';
  submitButtonLabel = 'Add KPI';

  // ✅ from DB only
  records: RoutineRecord[] = [];
  editingId: number | null = null;

  loading = false;
  saving = false;
  errorMessage = '';

  // ✅ backend base url (change port if needed)
  private readonly apiBase = `${environment.apiUrl}/mtnc-routine`;

  form = this.fb.group({
    kpi: ['', Validators.required],
    target: ['', Validators.required],
    calculation: ['', Validators.required],
    platform: [''],
    responsibleDGM: ['', Validators.required],
    definedOLADetails: [''],
    dataSources: ['']
  });

  ngOnInit(): void {
    // Load the routine maintenance definitions when the page opens.
    this.fetchData();
  }

  fetchData(): void {
    // Read, sort, and expose the database records while finalizing the loading indicator.
    this.loading = true;
    this.errorMessage = '';

    this.http
      .get<RoutineRecord[]>(this.apiBase)
      .pipe(finalize(() => {
        this.loading = false;
        this.cdr.detectChanges();
      }))
      .subscribe({
        next: (response) => {
          this.records = (response ?? []).sort((a, b) => a._id - b._id);
        },
        error: (err) => {
          console.error('Failed to fetch data', err);
          this.errorMessage = 'Unable to load routine maintenance KPIs from database.';
          this.records = [];
        }
      });
  }

  onSubmit(): void {
    // Validate the form and send normalized fields through the add/update endpoint.
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // ✅ normalize payload
    const payload = {
      kpi: (this.form.value.kpi ?? '').trim(),
      target: (this.form.value.target ?? '').trim(),
      calculation: (this.form.value.calculation ?? '').trim(),
      platform: '-',
      responsibleDGM: (this.form.value.responsibleDGM ?? '').trim(),
      definedOLADetails: '-',
      dataSources: '-'
    };

    const request$ = this.editingId
      ? this.http.put(`${this.apiBase}/update/${this.editingId}`, payload)
      : this.http.post(`${this.apiBase}/add`, payload);

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
          console.error('Failed to save data', err);
          this.errorMessage = err?.error?.message || 'Saving failed. Please try again.';
        }
      });
  }

  onEdit(record: RoutineRecord): void {
    // Populate the form with the selected definition and switch to update labels.
    this.editingId = record._id;
    this.form.patchValue({
      kpi: record.kpi ?? '',
      target: record.target ?? '',
      calculation: record.calculation ?? '',
      platform: record.platform ?? '',
      responsibleDGM: record.responsibleDGM ?? '',
      definedOLADetails: record.definedOLADetails ?? '',
      dataSources: record.dataSources ?? ''
    });
    this.formTitle = 'Update KPI';
    this.submitButtonLabel = 'Update KPI';
    this.errorMessage = '';
  }

  onDelete(id: number): void {
    // Validate and confirm the record ID before deleting the definition.
    if (!id || id <= 0) {
      this.errorMessage = 'Invalid record id.';
      return;
    }
    if (!window.confirm('Are you sure you want to delete this item?')) return;

    this.saving = true;
    this.errorMessage = '';

    this.http
      .delete(`${this.apiBase}/delete/${id}`)
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: () => {
          if (this.editingId === id) this.resetForm();
          this.fetchData();
        },
        error: (err) => {
          console.error('Failed to delete data', err);
          this.errorMessage = err?.error?.message || 'Deletion failed. Please try again.';
        }
      });
  }

  onCancelEdit(): void {
    // Return to add mode without persisting the current form values.
    this.resetForm();
    this.formTitle = 'Add KPI';
    this.submitButtonLabel = 'Add KPI';
    this.errorMessage = '';
  }

  private resetForm(): void {
    // Restore blank controls and the default add-operation labels.
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
    this.formTitle = 'Add KPI';
    this.submitButtonLabel = 'Add KPI';
  }
}
