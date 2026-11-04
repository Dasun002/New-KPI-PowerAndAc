/*
 File: final-table.component.ts
 Description: KPI definitions final table management page
 Purpose: CRUD operations for KPI definition setup including weightage calculation.
 Features: KPI definition management, point allocation, metric tracking
*/

import { CommonModule } from '@angular/common';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import { environment } from '../../../../../environments/environment';
import { AuthService } from '../../../../services/auth.service';

/* ========== DATA TYPES ========== */

/* KPI definition entity */
export type KpiDefinition = {
  id: number;
  perspectives: string;
  category?: string;
  strategicObjectives: string;
  keyPerformanceIndicators: string;
  unit: string;
  descriptionOfKPI: string;

  // ✅ backend calculated
  weightage: number; // decimal % e.g. 12.3456

  // ✅ user input
  pointsApplicable: number;
  totalPoints?: number;

  engineerResponsible?: string;
  contactNo?: string;

  month?: number;
  year?: number;
};

// ✅ REQUEST: remove weightage (backend calculates)
export type UpsertKpiDefinitionRequest = {
  perspectives: string;
  strategicObjectives: string;
  keyPerformanceIndicators: string;
  unit: string;
  descriptionOfKPI: string;

  pointsApplicable: number;
  totalPoints: number;
  category?: string;
  engineerResponsible?: string;
  contactNo?: string;
  weightage: number;

  month: number;
  year: number;
};

@Component({
  selector: 'app-final-table',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, HttpClientModule],
  templateUrl: './final-table.component.html',
  styleUrls: ['./final-table.component.scss'],
})
export class FinalTableComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly fb = inject(FormBuilder);
  private readonly cdr = inject(ChangeDetectorRef);
  private readonly authService = inject(AuthService);
  private readonly totalPointsStorageKey = 'kpi.final-table.totalPoints';

  pageTitle = 'Strategic KPI Management';

  records: KpiDefinition[] = [];
  editingId: number | null = null;

  loading = false;
  saving = false;
  errorMessage = '';
  isAdmin = false;

  private readonly apiBase = `${environment.apiUrl}/kpi-definitions`;

  // ✅ weightage is displayed but NOT editable, so keep a disabled control
  form = this.fb.nonNullable.group({
    perspectives: ['', [Validators.required]],
    category: ['', []],
    strategicObjectives: ['', [Validators.required]],
    keyPerformanceIndicators: ['', [Validators.required]],
    unit: ['', [Validators.required]],
    descriptionOfKPI: ['', [Validators.required]],

    // ✅ engineer responsible
    engineerResponsible: ['', []],

    // ✅ contact number
    contactNo: ['', []],

    // ✅ calculated field (readonly)
    weightage: this.fb.nonNullable.control({ value: 0, disabled: true }),

    // ✅ user input
    pointsApplicable: [0, [Validators.required, Validators.min(0)]],
    totalPoints: [36000, [Validators.required, Validators.min(1)]],
  });

  ngOnInit(): void {
    // Determine edit privileges, restore the saved total-points denominator, and load definitions.
    const role = this.authService.getRole();
    this.isAdmin = role === 'Admin' || role === 'SuperAdmin';
    this.form.patchValue({ totalPoints: this.getPersistedTotalPoints() });
    this.form.patchValue({ category: '' });
    this.fetchData();
  }

  fetchData(): void {
    // Load and normalize definitions so API casing differences do not affect the template.
    this.loading = true;
    this.errorMessage = '';

    this.http
      .get<any[]>(this.apiBase)
      .pipe(finalize(() => {
        this.loading = false;
        this.cdr.detectChanges();
      }))
      .subscribe({
        next: (res) => {
          this.records = (res ?? []).map((item) => this.normalizeRecord(item)).sort((a, b) => a.id - b.id);

          // ✅ If editing, refresh the displayed weightage from server (after recalculation)
          if (this.editingId) {
            const current = this.records.find((x) => x.id === this.editingId);
            if (current) this.form.controls.weightage.setValue(current.weightage ?? 0);
          }
        },
        error: (err) => {
          console.error('GET /api/kpi-definitions failed:', err);
          this.errorMessage = 'Unable to load KPI definitions.';
          this.records = [];
        },
      });
  }

  onSubmit(): void {
    // Validate, build a trimmed payload, and choose the create/update endpoint.
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const payload = this.buildPayload();

    const request$ = this.editingId
      ? this.http.put(`${this.apiBase}/${this.editingId}`, payload)
      : this.http.post(this.apiBase, payload);

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
          console.error('Save failed:', err);
          this.errorMessage = 'Save failed. Check backend validation / API errors.';
        },
      });
  }

  onEdit(record: KpiDefinition): void {
    // Populate editable fields while preserving the server-calculated weightage display.
    this.editingId = record.id;

    // ✅ patchValue because weightage control is disabled
    this.form.patchValue({
      perspectives: record.perspectives ?? '',
      category: record.category ?? '',
      strategicObjectives: record.strategicObjectives ?? '',
      keyPerformanceIndicators: record.keyPerformanceIndicators ?? '',
      unit: record.unit ?? '',
      descriptionOfKPI: record.descriptionOfKPI ?? '',
      engineerResponsible: record.engineerResponsible ?? '',
      contactNo: record.contactNo ?? '',
      pointsApplicable: record.pointsApplicable ?? 0,
      totalPoints: record.totalPoints ?? 36000,
    });

    // ✅ set readonly calculated field
    this.form.controls.weightage.setValue(record.weightage ?? 0);

    setTimeout(() => {
      const formSection = document.querySelector('.form-section');
      if (formSection) {
        formSection.scrollIntoView({ behavior: 'smooth', block: 'center' });
      }
    }, 0);
  }

 onDelete(id: number): void {
    // Delete a definition and reload the list so backend weightage recalculation is visible.
    if (!window.confirm('Delete this KPI row?')) return;

    this.saving = true;
    this.errorMessage = '';

    this.http
      .delete(`${this.apiBase}/${id}`)
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: () => {
          // ✅ backend recalculated weightage for remaining rows
          this.fetchData();
        },
        error: (err) => {
          console.error('Delete failed:', err);
          this.errorMessage = 'Delete failed.';
        },
      });
  }

  onCancelEdit(): void {
    this.resetForm();
  }

  saveTotalPoints(): void {
    // Persist the denominator used by future forms and live weightage previews in this browser.
    const totalPoints = Number(this.form.get('totalPoints')?.value ?? 36000);
    if (totalPoints <= 0) {
      this.errorMessage = 'Total Points must be greater than 0.';
      return;
    }

    localStorage.setItem(this.totalPointsStorageKey, String(totalPoints));
    this.errorMessage = '';
  }

  private buildPayload(): UpsertKpiDefinitionRequest {
    // Convert the form model into the API shape and calculate the submitted weightage preview.
    const raw = this.form.getRawValue(); // includes disabled too (fine)
    const now = new Date();

    return {
      perspectives: raw.perspectives.trim(),
      category: (raw.category ?? '').trim(),
      strategicObjectives: raw.strategicObjectives.trim(),
      keyPerformanceIndicators: raw.keyPerformanceIndicators.trim(),
      unit: raw.unit.trim(),
      descriptionOfKPI: raw.descriptionOfKPI.trim(),

      // ✅ engineer responsible and contact
      engineerResponsible: (raw.engineerResponsible ?? '').trim(),
      contactNo: (raw.contactNo ?? '').trim(),

      // ✅ only input needed
      pointsApplicable: Number(raw.pointsApplicable),
      totalPoints: Number(raw.totalPoints),
      weightage: Number(((Number(raw.pointsApplicable) / Number(raw.totalPoints || 36000)) * 100).toFixed(4)),

      month: now.getMonth() + 1,
      year: now.getFullYear(),
    };
  }

  private resetForm(): void {
    // Clear edit state and restore the configured total-points default.
    this.form.reset({
      perspectives: '',
      category: '',
      strategicObjectives: '',
      keyPerformanceIndicators: '',
      unit: '',
      descriptionOfKPI: '',
      engineerResponsible: '',
      contactNo: '',
      pointsApplicable: 0,
      totalPoints: this.getPersistedTotalPoints(),
      weightage: 0, // will be set by backend after save
    });

    // ✅ because weightage is disabled, set it manually too
    this.form.controls.weightage.setValue(0);

    this.editingId = null;
    this.errorMessage = '';
  }

  // ✅ UI helper: show % with 4 decimals (same as DB)
  formatWeightage(val: number | null | undefined): string {
    // Format the persisted percentage with the precision used by the table.
    const n = Number(val ?? 0);
    return `${n.toFixed(4)}%`;
  }

  /** Display weightage normalized to total points (table display) */
  getComputedWeightageForRecord(record: KpiDefinition): string {
    // Calculate the table percentage from points so it remains understandable if totals change.
    const totalPoints = Number(record.totalPoints ?? 36000);
    if (totalPoints <= 0) return '0.0000%';
    const weightage = (Number(record.pointsApplicable ?? 0) / totalPoints) * 100;
    return `${weightage.toFixed(4)}%`;
  }

  /** Calculate live weightage preview based on current points input */
  calculateLiveWeightage(): string {
    // Preview the weightage while the administrator edits points, without saving anything.
    const pointsApplicable = Number(this.form.get('pointsApplicable')?.value ?? 0);
    const totalPoints = Number(this.form.get('totalPoints')?.value ?? 36000);

    if (totalPoints <= 0 || pointsApplicable <= 0) return '0.00%';
    const weightage = (pointsApplicable / totalPoints) * 100;
    return weightage.toFixed(2) + '%';
  }

  /** Get total points for display hint */
  getTotalPointsDisplay(): number {
    // Expose the active denominator for the form's helper text.
    return Number(this.form.get('totalPoints')?.value ?? 36000);
  }

  private normalizeRecord(raw: any): KpiDefinition {
    // Normalize camelCase/PascalCase API responses into the frontend definition shape.
    return {
      id: Number(raw?.id ?? raw?.Id ?? 0),
      perspectives: raw?.perspectives ?? raw?.Perspectives ?? '',
      category: raw?.category ?? raw?.Category ?? '',
      strategicObjectives: raw?.strategicObjectives ?? raw?.StrategicObjectives ?? '',
      keyPerformanceIndicators: raw?.keyPerformanceIndicators ?? raw?.KeyPerformanceIndicators ?? '',
      unit: raw?.unit ?? raw?.Unit ?? '',
      descriptionOfKPI: raw?.descriptionOfKPI ?? raw?.DescriptionOfKPI ?? '',
      engineerResponsible: raw?.engineerResponsible ?? raw?.EngineerResponsible ?? '',
      weightage: Number(raw?.weightage ?? raw?.Weightage ?? 0),
      pointsApplicable: Number(raw?.pointsApplicable ?? raw?.PointsApplicable ?? 0),
      totalPoints: Number(raw?.totalPoints ?? raw?.TotalPoints ?? 36000),
      month: raw?.month ?? raw?.Month,
      year: raw?.year ?? raw?.Year,
    };
  }

  private getPersistedTotalPoints(): number {
    // Read the browser-level denominator and fall back to the standard value when invalid.
    const savedValue = Number(localStorage.getItem(this.totalPointsStorageKey) ?? 36000);
    return savedValue > 0 ? savedValue : 36000;
  }
}
