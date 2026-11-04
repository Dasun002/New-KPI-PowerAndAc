import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Observable } from 'rxjs';
import { PowerAndACRecord, PowerAndACService, PowerAndACWriteRecord } from '../../../../services/power-and-ac.service';
import { FilterUtils } from '../../../../utils/filter.utils';

@Component({
  selector: 'app-power-ac-admin',
  standalone: true,
  imports: [CommonModule, FormsModule, ReactiveFormsModule],
  templateUrl: './power-ac-admin.component.html',
  styleUrls: ['./power-ac-admin.component.scss']
})
export class PowerAcAdminComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  readonly monthOptions = FilterUtils.getMonthOptions(new Date().getFullYear() - 1);
  get yearOptions(): number[] {
    return Array.from(new Set([
      ...FilterUtils.generatePlatformYearOptions(),
      ...this.allRecords.map(record => record.year),
      this.filterYear
    ])).sort((a, b) => a - b);
  }

  readonly form = this.fb.nonNullable.group({
    designation: ['', [Validators.required, Validators.maxLength(100)]],
    year: [new Date().getFullYear(), [Validators.required, Validators.min(2000), Validators.max(9999)]],
    month: [new Date().getMonth() + 1, [Validators.required, Validators.min(1), Validators.max(12)]],
    scheduled: [0, [Validators.required, Validators.min(0)]],
    attended: [0, [Validators.required, Validators.min(0)]],
    cumulative_Sched: [0, [Validators.required, Validators.min(0)]],
    cumulative_Achieved: [0, [Validators.required, Validators.min(0)]]
  });

  records: PowerAndACRecord[] = [];
  filterYear = new Date().getFullYear();
  editingId: number | null = null;
  loading = false;
  saving = false;
  deletingId: number | null = null;
  errorMessage = '';
  successMessage = '';
  private allRecords: PowerAndACRecord[] = [];

  constructor(
    private readonly service: PowerAndACService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadRecords();
  }

  loadRecords(): void {
    this.loading = true;
    this.errorMessage = '';
    this.service.getAll().subscribe({
      next: records => {
        this.allRecords = records;
        this.records = this.allRecords.filter(record => record.year === this.filterYear);
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.errorMessage = 'Failed to load Power & A/C records.';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.saving) return;

    const payload: PowerAndACWriteRecord = this.form.getRawValue();
    this.saving = true;
    this.errorMessage = '';
    this.successMessage = '';
    const editingId = this.editingId;
    const action = editingId === null ? 'create' : 'update';
    let request: Observable<PowerAndACRecord>;
    if (editingId === null) {
      request = this.service.create(payload);
    } else {
      request = this.service.update(editingId, payload);
    }

    request.subscribe({
      next: () => {
        this.saving = false;
        this.successMessage = this.editingId === null ? 'Power & A/C record added.' : 'Power & A/C record updated.';
        this.filterYear = payload.year;
        this.resetForm();
        this.loadRecords();
      },
      error: (response: HttpErrorResponse) => {
        this.saving = false;
        this.errorMessage = this.getRequestError(response, action);
        this.cdr.detectChanges();
      }
    });
  }

  edit(record: PowerAndACRecord): void {
    this.editingId = record.id;
    this.form.setValue({
      designation: record.designation,
      year: record.year,
      month: record.month,
      scheduled: record.scheduled,
      attended: record.attended,
      cumulative_Sched: record.cumulative_Sched,
      cumulative_Achieved: record.cumulative_Achieved
    });
    this.successMessage = '';
  }

  delete(record: PowerAndACRecord): void {
    if (!confirm(`Delete the ${record.designation} record for ${record.year}-${record.month}?`)) return;
    this.errorMessage = '';
    this.successMessage = '';
    this.deletingId = record.id;
    this.service.delete(record.id).subscribe({
      next: () => {
        this.deletingId = null;
        if (this.editingId === record.id) this.resetForm();
        this.successMessage = 'Power & A/C record deleted.';
        this.loadRecords();
      },
      error: (error: HttpErrorResponse) => {
        this.deletingId = null;
        this.errorMessage = this.getRequestError(error, 'delete');
        this.cdr.detectChanges();
      }
    });
  }

  cancelEdit(): void {
    this.resetForm();
  }

  onYearChange(): void {
    this.records = this.allRecords.filter(record => record.year === this.filterYear);
  }

  monthLabel(month: number): string {
    return this.monthOptions.find(option => option.value === month)?.label ?? String(month);
  }

  private resetForm(): void {
    this.editingId = null;
    this.form.reset({
      designation: '',
      year: new Date().getFullYear(),
      month: new Date().getMonth() + 1,
      scheduled: 0,
      attended: 0,
      cumulative_Sched: 0,
      cumulative_Achieved: 0
    });
  }

  private getRequestError(error: HttpErrorResponse, action: string): string {
    if (error.status === 0) {
      return `Could not ${action} the record because the backend is unreachable.`;
    }
    if (error.status === 401) {
      return 'Your session has expired. Sign in again before changing Power & A/C records.';
    }
    if (error.status === 403) {
      return 'Your account is not authorized to change Power & A/C records.';
    }
    const body = error.error as { message?: string; detail?: string; title?: string } | null;
    const serverMessage = body?.message || body?.detail || body?.title;
    if (serverMessage) {
      return serverMessage;
    }
    if (error.status === 409) {
      return 'The database rejected the change because it conflicts with an existing record.';
    }
    if (error.status === 400) {
      return 'The server rejected the record values. Check all fields and try again.';
    }
    if (error.status >= 500) {
      return `The database could not ${action} the record. Check the backend log for the database error.`;
    }
    return `Could not ${action} the record (HTTP ${error.status}).`;
  }
}
