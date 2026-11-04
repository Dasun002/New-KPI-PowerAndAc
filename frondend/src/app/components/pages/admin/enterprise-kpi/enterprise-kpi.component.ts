/*
 File: enterprise-kpi.component.ts
 Description: Enterprise KPI admin management
 Purpose: CRUD operations for Enterprise KPI definitions.
*/

import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, Validators } from '@angular/forms';
import { EnterpriseKpiService, EnterpriseKpiRecord, CreateEnterpriseKpi, EnterpriseTargetDto, CreateEnterpriseTargetDto } from '../../../../services/enterprise-kpi.service';
import { FilterUtils } from '../../../../utils/filter.utils';

@Component({
  selector: 'app-enterprise-kpi',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule],
  templateUrl: './enterprise-kpi.component.html',
  styleUrls: ['./enterprise-kpi.component.scss']
})
export class EnterpriseKpiComponent implements OnInit {

  pageTitle = 'Enterprise KPI';

  records: EnterpriseKpiRecord[] = [];
  editingId: number | null = null;

  loading = false;
  saving = false;
  errorMessage = '';

  form!: any;

  // Target values are selected by month/year independently of the KPI record list.
  selectedMonth: number = new Date().getMonth() + 1;
  selectedYear: number = new Date().getFullYear();
  get monthOptions() { return FilterUtils.getMonthOptions(this.selectedYear); }
  yearOptions: number[] = FilterUtils.generateYearOptions();
  allTargets: EnterpriseTargetDto[] = [];
  targetEditValues: { [kpiId: number]: string } = {};
  targetSaving: { [kpiId: number]: boolean } = {};
  targetsExpanded = true;

  // Injects the required dependencies.
  constructor(
    private fb: FormBuilder,
    private service: EnterpriseKpiService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    // Build the KPI form and load both KPI records and period-specific targets.
    this.form = this.fb.group({
      networkEngineerKpi: ['', Validators.required],
      division: [''],
      section: [''],
      kpiPercent: ['']
    });

    this.fetchData();
    this.loadTargets();
  }

  fetchData(): void {
    // Load the Enterprise KPI definitions used by the table and target editor.
    this.loading = true;
    this.service.getAll().subscribe({
      next: (res) => { this.loading = false; this.records = res; this.cdr.detectChanges(); },
      error: () => { this.loading = false; this.errorMessage = 'Failed to load KPI data'; this.cdr.detectChanges(); }
    });
  }

  onSubmit(): void {
    // Build the API DTO and route the request to create or update based on editingId.
    if (this.form.invalid) return;

    const payload: CreateEnterpriseKpi = {
      networkEngineerKpi: this.form.value.networkEngineerKpi,
      division: this.form.value.division || undefined,
      section: this.form.value.section || undefined,
      kpiPercent: this.form.value.kpiPercent ? Number(this.form.value.kpiPercent) : undefined
    };

    this.saving = true;

    if (this.editingId) {
      this.service.update(this.editingId, payload).subscribe({
        next: () => { this.saving = false; this.resetForm(); this.fetchData(); this.cdr.detectChanges(); },
        error: () => { this.saving = false; this.errorMessage = 'Save failed'; this.cdr.detectChanges(); }
      });
    } else {
      this.service.create(payload).subscribe({
        next: () => { this.saving = false; this.resetForm(); this.fetchData(); this.cdr.detectChanges(); },
        error: () => { this.saving = false; this.errorMessage = 'Save failed'; this.cdr.detectChanges(); }
      });
    }
  }

  onEdit(record: EnterpriseKpiRecord): void {
    // Copy a table row into the form and switch the submit flow to update mode.
    this.editingId = record.id;
    this.form.patchValue({
      networkEngineerKpi: record.networkEngineerKpi,
      division: record.division || '',
      section: record.section || '',
      kpiPercent: record.kpiPercent || ''
    });
  }

  onDelete(id: number): void {
    // Confirm deletion, then refresh the table so the removed definition disappears.
    if (!confirm('Delete this record?')) return;
    this.saving = true;
    this.service.delete(id).subscribe({
      next: () => { this.saving = false; this.fetchData(); this.cdr.detectChanges(); },
      error: () => { this.saving = false; this.errorMessage = 'Delete failed'; this.cdr.detectChanges(); }
    });
  }

  onCancelEdit(): void { this.resetForm(); }

  private resetForm(): void { this.form.reset(); this.editingId = null; }

  // =========================
  // TARGET ASSIGNMENT
  // =========================
  toggleTargetsExpanded(): void {
    // Expand or collapse the inline target assignment section.
    this.targetsExpanded = !this.targetsExpanded;
  }

  onTargetPeriodChange(): void {
    // Recalculate the displayed target values for the newly selected month/year.
    this.populateTargetEditValues();
  }

  loadTargets(): void {
    // Load all target rows once; getTargetForKpi selects the active period locally.
    this.service.getTargets().subscribe({
      next: (data) => {
        this.allTargets = data;
        this.populateTargetEditValues();
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Failed to load targets', err);
      }
    });
  }

  populateTargetEditValues(): void {
    // Initialize each editor with the target matching the current KPI and period.
    this.targetEditValues = {};
    for (const record of this.records) {
      const target = this.getTargetForKpi(record.id);
      this.targetEditValues[record.id] = target?.section || '';
    }
  }

  getTargetForKpi(kpiId: number): EnterpriseTargetDto | undefined {
    // Match a target by both KPI identity and reporting period.
    return this.allTargets.find(t => 
      t.enterpriseKpiId === kpiId && 
      t.month === Number(this.selectedMonth) && 
      t.year === Number(this.selectedYear)
    );
  }

  saveTarget(kpiId: number): void {
    // Upsert the target for the selected KPI and period, updating local state after success.
    const val = this.targetEditValues[kpiId];

    this.targetSaving[kpiId] = true;
    const existing = this.getTargetForKpi(kpiId);

    const payload: CreateEnterpriseTargetDto = {
      enterpriseKpiId: kpiId,
      section: val,
      month: Number(this.selectedMonth),
      year: Number(this.selectedYear)
    };

    if (existing) {
      this.service.updateTarget(existing.id, payload).subscribe({
        next: () => {
          existing.section = val;
          this.targetSaving[kpiId] = false;
          this.cdr.detectChanges();
          alert('Target updated successfully.');
        },
        error: (err) => {
          console.error(err);
          this.targetSaving[kpiId] = false;
          alert('Failed to update target.');
          this.cdr.detectChanges();
        }
      });
    } else {
      this.service.createTarget(payload).subscribe({
        next: (newTarget) => {
          this.allTargets.push(newTarget);
          this.targetSaving[kpiId] = false;
          this.cdr.detectChanges();
          alert('Target created successfully.');
        },
        error: (err) => {
          console.error(err);
          this.targetSaving[kpiId] = false;
          alert('Failed to create target.');
          this.cdr.detectChanges();
        }
      });
    }
  }
}
