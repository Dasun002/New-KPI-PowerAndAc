/*
 File: service-fulfilment.component.ts
 Description: Service fulfilment KPI admin management
 Purpose: CRUD operations for service fulfilment KPI metrics and thresholds.
 Features: Data table management, metrics configuration, form handling
*/

import { Component, OnInit, inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule, DOCUMENT } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { ServiceFulfilmentKpiDto, ServiceFulfilmentKpiService } from '../../../../services/service-fulfilment-kpi.service';

/* ========== ADMIN SERVICE FULFILMENT COMPONENT ========== */

/* Service fulfilment KPI row with display order */
type AdminKpiRow = ServiceFulfilmentKpiDto & { displayOrder?: number };

@Component({
  selector: 'app-admin-service-fulfilment',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './service-fulfilment.component.html',
  styleUrls: ['./service-fulfilment.component.scss']
})
export class AdminServiceFulfilmentComponent implements OnInit {
  private readonly defaultMonth = 11;
  private readonly defaultYear = 2025;
  private readonly document = inject(DOCUMENT);
  private readonly cdr = inject(ChangeDetectorRef);

  // Page heading shown above the management table.
  pageTitle = 'Service Fulfilment';


  // Form state shared by add and edit operations.
  kpiForm!: FormGroup;
  isEditing = false;
  editingId: number | string | null = null;
  showForm = false;

  // Database rows plus a frontend-only display order.
  kpiList: AdminKpiRow[] = [];

  loading = false;
  saving = false;
  errorMessage = '';

  // Injects the required dependencies.
  constructor(
    private fb: FormBuilder,
    private serviceFulfilmentKpiService: ServiceFulfilmentKpiService
  ) { }

  ngOnInit(): void {
    // Create the form with required KPI metadata and the default reporting period.
    this.kpiForm = this.fb.group({
      kpi: ['', Validators.required],
      target: ['', Validators.required],
      calculation: ['', Validators.required],
      platform: [''],
      responsibleDgm: [''],
      definedOla: [''],
      weightage: [0, Validators.required],
      dataSources: [''],
      month: [this.defaultMonth, Validators.required],
      year: [this.defaultYear, Validators.required]
    });

    this.loadKpis();
  }

  // ================= LOAD =================
  loadKpis(): void {
    // Load rows and assign a fallback display order when the API omits one.
    this.loading = true;

    this.serviceFulfilmentKpiService.getAll().subscribe({
      next: data => {
        this.kpiList = data.map((kpi, index) => ({
          ...kpi,
          displayOrder: kpi.displayOrder ?? index + 1
        }));
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: err => {
        console.error(err);
        this.errorMessage = 'Failed to load KPI data';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  // ================= ADD =================
  addNewKpi(): void {
    // Prepare a clean add state and bring the form into view.
    this.isEditing = false;
    this.editingId = null;
    this.resetForm();
    this.showForm = true;
    this.scrollFormIntoView();
  }

  // ================= SUBMIT =================
  onSubmit(): void {
    // Convert form values to the service DTO and select add or update based on edit state.
    if (this.kpiForm.invalid) {
      this.kpiForm.markAllAsTouched();
      return;
    }

    const formValue = this.kpiForm.value;
    const definedOlaValue = (formValue.definedOla ?? '').toString().trim();

    const payload: ServiceFulfilmentKpiDto = {
      id: this.isEditing && this.editingId ? this.editingId : undefined,
      kpi: formValue.kpi,
      target: formValue.target,
      calculation: formValue.calculation,
      platform: formValue.platform || '-',
      responsibleDgm: formValue.responsibleDgm || '-',
      defineDoladetails: formValue.definedOla || '-',
      definedoladetails: formValue.definedOla || '-',
      dataSources: formValue.dataSources || '-',
      weightage: Number(formValue.weightage),
      month: Number(formValue.month),
      year: Number(formValue.year)
    };

    this.saving = true;

    const request$ = this.isEditing && this.editingId
      ? this.serviceFulfilmentKpiService.update(this.editingId, payload)
      : this.serviceFulfilmentKpiService.add(payload);

    request$.subscribe({
      next: () => {
        this.resetForm();
        this.loadKpis();
        this.showForm = false;
        this.cdr.detectChanges();
      },
      error: err => {
        console.error(err);
        this.errorMessage = 'Save failed';
        this.saving = false;
        this.cdr.detectChanges();
      },
      complete: () => this.saving = false
    });
  }

  // ================= EDIT =================
  editKpi(index: number): void {
    // Load one table row into the form, including compatibility handling for OLA field aliases.
    const kpi = this.kpiList[index];
    this.isEditing = true;
    this.editingId = kpi.id ?? (kpi as any).Id;
    const definedOlaValue = this.resolveDefinedOlaValue(kpi);

    this.kpiForm.patchValue({
      kpi: kpi.kpi ?? '',
      target: kpi.target ?? '',
      calculation: kpi.calculation ?? '',
      platform: kpi.platform ?? '',
      responsibleDgm: kpi.responsibleDgm ?? '',
      definedOla: definedOlaValue ?? '',
      weightage: kpi.weightage ?? 0,
      dataSources: kpi.dataSources ?? '',
      month: kpi.month ?? this.defaultMonth,
      year: kpi.year ?? this.defaultYear
    });
    this.showForm = true;
    this.scrollFormIntoView();
  }

  // ================= DELETE =================
  deleteKpi(index: number): void {
    // Confirm deletion by the row's backend identifier, then refresh the list.
    const kpi = this.kpiList[index];
    const id = kpi.id ?? (kpi as any).Id;

    if (!id || !confirm('Delete this KPI?')) return;

    this.serviceFulfilmentKpiService.delete(id).subscribe({
      next: () => {
        this.loadKpis();
        this.cdr.detectChanges();
      },
      error: err => {
        console.error(err);
        this.errorMessage = 'Delete failed';
        this.cdr.detectChanges();
      }
    });
  }

  // ================= RESET =================
  resetForm(): void {
    // Clear edit state while retaining the default month/year values.
    this.kpiForm.reset({
      month: this.defaultMonth,
      year: this.defaultYear
    });
    this.isEditing = false;
    this.editingId = null;
  }

  toggleForm(): void {
    // Toggle between the visible form and the compact table-only layout.
    if (this.showForm) {
      this.closeForm();
    } else {
      this.addNewKpi();
    }
  }

  closeForm(): void {
    // Hide the form and clear any unsaved edit values.
    this.showForm = false;
    this.resetForm();
  }

  private scrollFormIntoView(): void {
    // Delay scrolling until Angular has rendered the form after the visibility change.
    setTimeout(() => {
      if (!this.showForm) {
        return;
      }
      const element = this.document?.getElementById('service-fulfilment-form');
      element?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  }

  private resolveDefinedOlaValue(kpi?: Partial<ServiceFulfilmentKpiDto>): string {
    // Accept either backend spelling used historically for the defined OLA details field.
    if (!kpi) {
      return '';
    }
    const direct = (kpi as any).definedoladetails ?? (kpi as any).defineDoladetails;
    return typeof direct === 'string' ? direct : '';
  }
}
