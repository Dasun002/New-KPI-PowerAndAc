/*
 File: otn-op-2.component.ts
 Description: OTN Operations 2 KPI admin management
 Purpose: CRUD operations for OTN OP2 KPI metrics and performance targets.
 Features: Data table management, add/edit/delete operations, form validation
*/

import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { OtnOp2Service, OtnOpKpi, CreateOtnOpKpi } from '../../../../services/otn-op2.service';

/* ========== OTN OP2 ADMIN COMPONENT ========== */

@Component({
  selector: 'app-otn-op-2',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './otn-op-2.component.html',
  styleUrls: ['./otn-op-2.component.scss']
})
export class OtnOp2Component implements OnInit {

  private readonly fb = inject(FormBuilder);
  private readonly otnOp2Service = inject(OtnOp2Service);
  private readonly cdr = inject(ChangeDetectorRef);

  pageTitle = 'OTN OP - 02';

  records: OtnOpKpi[] = [];
  editingId: number | null = null;

  loading = false;
  saving = false;
  errorMessage = '';

  form = this.fb.group({
    networkEngineerKpi: ['', Validators.required],
    division: [''],
    section: [''],
    kpiPercent: [null as number | null]
  });

  ngOnInit(): void {
    // Load the OTN OP2 definitions after the component form has been created.
    this.fetchData();
  }

  fetchData(): void {
    // Retrieve records and keep the loading state synchronized with the observable lifecycle.
    this.loading = true;

    this.otnOp2Service.getAllKpis().subscribe({
      next: data => {
        this.records = data;
        this.cdr.detectChanges();
      },
      error: err => {
        console.error(err);
        this.errorMessage = 'Failed to load KPI data';
        this.loading = false;
        this.cdr.detectChanges();
      },
      complete: () => {
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  onSubmit(): void {
    // Validate the form, build the DTO, and route it to update or create.
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const payload: CreateOtnOpKpi = {
      networkEngineerKpi: this.form.value.networkEngineerKpi!,
      division: this.form.value.division || undefined,
      section: this.form.value.section || undefined,
      kpiPercent: this.form.value.kpiPercent ?? undefined
    };

    this.saving = true;

    if (this.editingId) {
      this.otnOp2Service.updateKpi(this.editingId, payload).subscribe({
        next: () => {
          this.resetForm();
          this.fetchData();
          this.saving = false;
          this.cdr.detectChanges();
        },
        error: (err: any) => {
          console.error(err);
          this.errorMessage = 'Save failed';
          this.saving = false;
          this.cdr.detectChanges();
        }
      });
    } else {
      this.otnOp2Service.createKpi(payload).subscribe({
        next: () => {
          this.resetForm();
          this.fetchData();
          this.saving = false;
          this.cdr.detectChanges();
        },
        error: (err: any) => {
          console.error(err);
          this.errorMessage = 'Save failed';
          this.saving = false;
          this.cdr.detectChanges();
        }
      });
    }
  }

  onEdit(record: OtnOpKpi): void {
    // Copy the selected record into the form while preserving nullable KPI percentage values.
    this.editingId = record.id;

    this.form.patchValue({
      networkEngineerKpi: record.networkEngineerKpi,
      division: record.division || '',
      section: record.section || '',
      kpiPercent: record.kpiPercent ?? null
    });
  }

  onDelete(id?: number): void {
    // Confirm deletion and refresh the table after the API request completes.
    if (!id || !confirm('Delete this record?')) return;

    this.saving = true;

    this.otnOp2Service.deleteKpi(id).subscribe({
      next: () => {
        this.fetchData();
        this.cdr.detectChanges();
      },
      error: err => {
        console.error(err);
        this.errorMessage = 'Delete failed';
        this.saving = false;
        this.cdr.detectChanges();
      },
      complete: () => {
        this.saving = false;
        this.cdr.detectChanges();
      }
    });
  }

  onCancelEdit(): void {
    // Discard unsaved changes and leave edit mode.
    this.resetForm();
  }

  private resetForm(): void {
    // Reset controls to their initial values and clear the edit identifier.
    this.form.reset({
      networkEngineerKpi: '',
      division: '',
      section: '',
      kpiPercent: null
    });
    this.editingId = null;
  }
}
