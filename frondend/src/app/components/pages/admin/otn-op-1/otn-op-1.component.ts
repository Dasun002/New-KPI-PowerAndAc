/*
 File: otn-op-1.component.ts
 Description: OTN Operations 1 KPI admin management
 Purpose: CRUD operations for OTN OP1 KPI metrics and platform targets.
 Features: Data table management, add/edit/delete operations, form handling
*/

import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';

import { OtnOp1Service, OtnOpKpi, CreateOtnOpKpi } from '../../../../services/otn-op1.service';

/* ========== OTN OP1 ADMIN COMPONENT ========== */

@Component({
  selector: 'app-otn-op-1',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './otn-op-1.component.html',
  styleUrls: ['./otn-op-1.component.scss']
})
export class OtnOp1Component implements OnInit {

  pageTitle = 'OTN OP - 01';

  records: OtnOpKpi[] = [];
  editingId: number | null = null;

  loading = false;
  saving = false;
  errorMessage = '';

  form!: any;

  // Injects the required dependencies.
  constructor(
    private fb: FormBuilder,
    private otnOp1Service: OtnOp1Service,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    // Build the OTN OP1 form and load its existing KPI definitions.
    this.form = this.fb.group({
      networkEngineerKpi: ['', Validators.required],
      division: [''],
      section: [''],
      kpiPercent: ['']
    });

    this.fetchData();
  }

  fetchData(): void {
    // Retrieve the records used by the OTN OP1 management table.
    this.loading = true;
    this.otnOp1Service
      .getAllKpis()
      .subscribe({
        next: (res: OtnOpKpi[]) => {
          this.loading = false;
          this.records = res;
          this.cdr.detectChanges();
        },
        error: () => {
          this.loading = false;
          this.errorMessage = 'Failed to load KPI data';
          this.cdr.detectChanges();
        }
      });
  }

  onSubmit(): void {
    // Build the DTO and choose update or create according to editingId.
    if (this.form.invalid) return;

    const payload: CreateOtnOpKpi = {
      networkEngineerKpi: this.form.value.networkEngineerKpi,
      division: this.form.value.division || undefined,
      section: this.form.value.section || undefined,
      kpiPercent: this.form.value.kpiPercent ? Number(this.form.value.kpiPercent) : undefined
    };
    
    this.saving = true;

    if (this.editingId) {
      this.otnOp1Service.updateKpi(this.editingId, payload).subscribe({
        next: () => {
          this.saving = false;
          this.resetForm();
          this.fetchData();
          this.cdr.detectChanges();
        },
        error: () => {
          this.saving = false;
          this.errorMessage = 'Save failed';
          this.cdr.detectChanges();
        }
      });
    } else {
      this.otnOp1Service.createKpi(payload).subscribe({
        next: () => {
          this.saving = false;
          this.resetForm();
          this.fetchData();
          this.cdr.detectChanges();
        },
        error: () => {
          this.saving = false;
          this.errorMessage = 'Save failed';
          this.cdr.detectChanges();
        }
      });
    }
  }

  onEdit(record: OtnOpKpi): void {
    // Copy the selected definition into the form and enter edit mode.
    this.editingId = record.id;
    this.form.patchValue({
      networkEngineerKpi: record.networkEngineerKpi,
      division: record.division || '',
      section: record.section || '',
      kpiPercent: record.kpiPercent || ''
    });
  }

  onDelete(id: number): void {
    // Confirm deletion, then refresh the table after the service succeeds.
    if (!confirm('Delete this record?')) return;

    this.saving = true;
    this.otnOp1Service
      .deleteKpi(id)
      .subscribe({
        next: () => {
          this.saving = false;
          this.fetchData();
          this.cdr.detectChanges();
        },
        error: () => {
          this.saving = false;
          this.errorMessage = 'Delete failed';
          this.cdr.detectChanges();
        }
      });
  }

  onCancelEdit(): void {
    // Discard the current form values and return to add mode.
    this.resetForm();
  }

  private resetForm(): void {
    // Restore empty controls and clear the selected record identifier.
    this.form.reset();
    this.editingId = null;
  }
}
