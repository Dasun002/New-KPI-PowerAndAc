/*
 File: tower-mtce-achievement.component.ts
 Description: Tower maintenance achievement admin page
 Purpose: CRUD operations for tower maintenance KPI targets and performance.
 Features: Data table management, target configuration, achievement tracking
*/

import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../../environments/environment';

/* ========== DATA INTERFACES ========== */

/* Tower maintenance KPI entity */
interface TowerKpi {
  id: number;  // Changed from string to number (int identity)
  responsibility: string;
  frequency: string;
  weightage: string;
  kpi: string;
  month?: number;
  year?: number;
}

@Component({
  selector: 'app-tower-mtce-achievement',
  standalone: true,
  imports: [
    CommonModule,          // ngIf, ngFor
    ReactiveFormsModule    // formGroup
  ],
  templateUrl: './tower-mtce-achievement.component.html',
  styleUrls: ['./tower-mtce-achievement.component.scss']
})
export class TowerMtceAchievementComponent implements OnInit {
  pageTitle = 'Tower Maintenance Achievement';
  records: TowerKpi[] = [];

  form!: FormGroup;
  loading = false;
  saving = false;
  errorMessage = '';
  editingId: number | null = null;

  // MUST MATCH BACKEND CONTROLLER
  //private apiUrl = 'http://localhost:5043/api/kpitower';
  private apiUrl = `${environment.apiUrl}/kpitower`;
  constructor(
    private fb: FormBuilder,
    private http: HttpClient,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      responsibility: ['', Validators.required],
      frequency: ['', Validators.required],
      weightage: ['', Validators.required],
      kpi: ['', Validators.required],
      month: [''],
      year: ['']
    });

    this.loadData();
  }

  // =========================
  // LOAD DATA
  // =========================
  loadData(): void {
    this.loading = true;
    this.errorMessage = '';

    this.http.get<TowerKpi[]>(this.apiUrl).subscribe({
      next: (data: TowerKpi[]) => {
        this.records = data;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        this.errorMessage = 'Failed to load KPI data';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  // =========================
  // CREATE / UPDATE
  // =========================
 onSubmit(): void {
  if (this.form.invalid) return;

  this.saving = true;

  // ✅ FIX: normalize empty strings → null
  const payload = {
    ...this.form.value,
    month: this.form.value.month || null,
  year: this.form.value.year || null
  };

  if (this.editingId) {
    this.http.put(`${this.apiUrl}/${this.editingId}`, payload).subscribe({
      next: () => {
        this.resetForm();
        this.loadData();
      },
      error: (err: any) => {
        console.error(err);
        this.errorMessage = err?.error ?? 'Failed to update KPI';
        this.saving = false;
        this.cdr.detectChanges();
      }
    });
  } else {
    this.http.post(this.apiUrl, payload).subscribe({
      next: () => {
        this.resetForm();
        this.loadData();
      },
      error: (err: any) => {
        console.error(err);
        this.errorMessage = err?.error ?? 'Failed to add KPI';
        this.saving = false;
        this.cdr.detectChanges();
      }
    });
  }
}

  // =========================
  // EDIT
  // =========================
  onEdit(record: TowerKpi): void {
    this.editingId = record.id;
    this.form.patchValue(record);
  }

  onCancelEdit(): void {
    this.resetForm();
  }

  // =========================
  // DELETE
  // =========================
  onDelete(id: number): void {
    if (!confirm('Delete this KPI?')) return;

    this.saving = true;

    this.http.delete(`${this.apiUrl}/${id}`).subscribe({
      next: () => this.loadData(),
      error: (err: any) => {
        this.errorMessage = 'Failed to delete KPI';
        this.saving = false;
        this.cdr.detectChanges();
      }
    });
  }

  // =========================
  // HELPERS
  // =========================
  resetForm(): void {
    this.form.reset();
    this.editingId = null;
    this.saving = false;
  }
}
