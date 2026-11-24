/*
 File: tower-mtce-achievement.component.ts
 Description: Other Operator KPI admin management page (historically named tower-mtce-achievement)
 Purpose: CRUD operations for Other Operator KPI definitions.
*/

import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { OtherOperatorKpiService, OtherOperatorKpiRecord, CreateOtherOperatorKpi, OtherOperatorTargetDto, CreateOtherOperatorTargetDto } from '../../../../services/other-operator-kpi.service';
import { FilterUtils } from '../../../../utils/filter.utils';

@Component({
  selector: 'app-other-operator',

  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule
  ],
  templateUrl: './other-operator.component.html',
  styleUrls: ['./other-operator.component.scss']
})
export class OtherOperatorComponent implements OnInit {

  pageTitle = 'Other Operator';
  records: OtherOperatorKpiRecord[] = [];

  form!: FormGroup;
  loading = false;
  saving = false;
  errorMessage = '';
  editingId: number | null = null;

  // Target values are selected by month/year independently of the KPI record list.
  selectedMonth: number = new Date().getMonth() + 1;
  selectedYear: number = new Date().getFullYear();
  get monthOptions() { return FilterUtils.getMonthOptions(this.selectedYear); }
  yearOptions: number[] = FilterUtils.generateYearOptions();
  allTargets: OtherOperatorTargetDto[] = [];
  targetEditValues: { [kpiId: number]: string } = {};
  targetSaving: { [kpiId: number]: boolean } = {};
  targetsExpanded = true;

  // Injects the required dependencies.
  constructor(
    private fb: FormBuilder,
    private service: OtherOperatorKpiService,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    // Build the KPI form and load both records and period-specific targets.
    this.form = this.fb.group({
      networkEngineerKpi: ['', Validators.required],
      division: [''],
      section: [''],
      kpiPercent: ['']
    });

    this.loadData();
    this.loadTargets();
  }

  // =========================
  // LOAD DATA
  // =========================
  loadData(): void {
    // Fetch and sort KPI definitions before rendering the management table.
    this.loading = true;
    this.errorMessage = '';

    this.service.getAll().subscribe({
      next: (data: OtherOperatorKpiRecord[]) => {
        this.records = [...data].sort((a, b) => a.id - b.id);

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
    // Validate the form and route the payload to create or update.
    if (this.form.invalid) return;

    this.saving = true;
    const payload: CreateOtherOperatorKpi = {
      networkEngineerKpi: this.form.value.networkEngineerKpi,
      division: this.form.value.division || undefined,
      section: this.form.value.section || undefined,
      kpiPercent: this.form.value.kpiPercent ? Number(this.form.value.kpiPercent) : undefined
    };

    if (this.editingId) {
      this.service.update(this.editingId, payload).subscribe({
        next: () => {
          this.resetForm();
          this.loadData();
        },
        error: (err: any) => {
          console.error(err);
          this.errorMessage = 'Failed to update KPI';
          this.saving = false;
          this.cdr.detectChanges();
        }
      });
    } else {
      this.service.create(payload).subscribe({
        next: () => {
          this.resetForm();
          this.loadData();
        },
        error: (err: any) => {
          console.error(err);
          this.errorMessage = 'Failed to add KPI';
          this.saving = false;
          this.cdr.detectChanges();
        }
      });
    }
  }

  // =========================
  // EDIT
  // =========================
  onEdit(record: OtherOperatorKpiRecord): void {
    // Copy a table row into the form and switch the next submit to update mode.
    this.editingId = record.id;
    this.form.patchValue({
      networkEngineerKpi: record.networkEngineerKpi,
      division: record.division || '',
      section: record.section || '',
      kpiPercent: record.kpiPercent || ''
    });
  }

  onCancelEdit(): void {
    this.resetForm();
  }

  // =========================
  // DELETE
  // =========================
  onDelete(id: number): void {
    // Confirm deletion, then reload the table after the service completes it.
    if (!confirm('Delete this KPI?')) return;

    this.saving = true;

    this.service.delete(id).subscribe({
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
    // Clear the form, edit identifier, and save state after CRUD actions.
    this.form.reset();
    this.editingId = null;
    this.saving = false;
  }

  // =========================
  // TARGET ASSIGNMENT
  // =========================
  toggleTargetsExpanded(): void {
    // Expand or collapse the inline target assignment section.
    this.targetsExpanded = !this.targetsExpanded;
  }

  onTargetPeriodChange(): void {
    // Recalculate which target values are shown for the selected period.
    this.populateTargetEditValues();
  }

  loadTargets(): void {
    // Load all targets once; the KPI/period lookup is performed locally.
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
    // Seed each target editor with the matching value for the current KPI and period.
    this.targetEditValues = {};
    for (const record of this.records) {
      const target = this.getTargetForKpi(record.id);
      this.targetEditValues[record.id] = target?.section || '';
    }
  }

  getTargetForKpi(kpiId: number): OtherOperatorTargetDto | undefined {
    // Match a target using both the KPI ID and the selected month/year.
    return this.allTargets.find(t => 
      t.otherOperatorKpiId === kpiId && 
      t.month === Number(this.selectedMonth) && 
      t.year === Number(this.selectedYear)
    );
  }

  saveTarget(kpiId: number): void {
    // Upsert the target for this KPI and period, then update the local target collection.
    const val = this.targetEditValues[kpiId];
    // if (!val) return; // Allow empty string to save empty target if needed

    this.targetSaving[kpiId] = true;
    const existing = this.getTargetForKpi(kpiId);

    const payload: CreateOtherOperatorTargetDto = {
      otherOperatorKpiId: kpiId,
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

