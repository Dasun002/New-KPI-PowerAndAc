/*
 File: tm-activity-plan.component.ts
 Description: TM Activity Plan KPI admin management
 Purpose: CRUD operations for TM activity plan KPI metrics.
 Features: Data table management, activity tracking, form validation
*/

import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { finalize } from 'rxjs/operators';

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
  selector: 'app-admin-tm-activity-plan',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, HttpClientModule],
  templateUrl: './tm-activity-plan.component.html',
  styleUrls: ['./tm-activity-plan.component.scss']
})
export class AdminTmActivityPlanComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  private readonly cdr = inject(ChangeDetectorRef);

  pageTitle = 'TM Activity Plan';
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
    responsibleDGM: ['', Validators.required],
    definedOLADetails: ['', Validators.required],
    dataSources: ['', Validators.required]
  });

  ngOnInit(): void {
    this.fetchData();
  }

  fetchData(): void {
    this.loading = true;
    this.errorMessage = '';
    this.http
      .get<ActivityRecord[]>('https://socapps.intranet.slt.com.lk/kpi/api/TmActivityPlans') // Connect to real API
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
          this.errorMessage = 'Unable to load TM activities.';
        }
      });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const payload = this.form.getRawValue();
    const request$ = this.editingId
      ? this.http.put(`https://socapps.intranet.slt.com.lk/kpi/api/TmActivityPlans/${this.editingId}`, { id: this.editingId, ...payload })
      : this.http.post('https://socapps.intranet.slt.com.lk/kpi/api/TmActivityPlans', payload);

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
    if (!window.confirm('Are you sure you want to delete this item?')) {
      return;
    }

    this.saving = true;
    this.http
      .delete(`https://socapps.intranet.slt.com.lk/kpi/api/TmActivityPlans/${id}`)
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
    this.resetForm();
  }

  private resetForm(): void {
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

