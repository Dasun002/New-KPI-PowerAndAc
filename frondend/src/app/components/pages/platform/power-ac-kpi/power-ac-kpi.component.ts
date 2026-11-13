/* File: power-ac-kpi.component.ts
   Power & A/C KPI page. One table (dbo.PowerAcPmData) holds every value: the Preventive Maintenance,
   Critical Safety, Corrective Action and Availability Achievement %, and the area allocation.
   The overview shows the calculated results. The two input grids save values and verify them. */
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../../../services/auth.service';
import {
  PowerAcDataRow,
  PowerAcKpiService,
  PowerAcResults,
  PowerAcSaveItem
} from '../../../../services/power-ac-kpi.service';

@Component({
  selector: 'app-power-ac-kpi',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './power-ac-kpi.component.html',
  styleUrls: ['../tm-activity-plan/tm-activity-plan.component.scss', './power-ac-kpi.component.scss']
})
export class PowerAcKpiComponent implements OnInit {
  readonly months = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
  readonly yearOptions = [2025, 2026, 2027, 2028];
  readonly pmCode = 'PM';

  year = new Date().getFullYear();
  month = new Date().getMonth() + 1;
  rows: PowerAcDataRow[] = [];
  results: PowerAcResults | null = null;
  loading = false;
  busy = false;
  message = '';
  error = '';
  canEdit = false;
  canVerify = false;

  // Edited values per engineer group (designation) and KPI code, and the groups with unsaved edits.
  private draft = new Map<string, Record<string, number | null>>();
  private dirty = new Set<string>();

  constructor(
    private readonly api: PowerAcKpiService,
    private readonly auth: AuthService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const role = (this.auth.getRole() ?? '').replace(/\s+/g, '').toLowerCase();
    const pageEditor = this.auth.canEditPage('Power & A/C');
    this.canEdit = role === 'admin' || role === 'superadmin' || pageEditor;
    this.canVerify = role === 'superadmin' || pageEditor;
    this.load();
  }

  onPeriodChange(): void {
    this.load();
  }

  // KPIs other than Preventive Maintenance, in configuration order. Each gets a row in the middle grid.
  get otherComponents() {
    return (this.results?.components ?? []).filter(c => c.code !== this.pmCode);
  }

  get pmComponent() {
    return (this.results?.components ?? []).find(c => c.code === this.pmCode) ?? null;
  }

  load(): void {
    this.loading = true;
    this.error = '';
    this.message = '';
    forkJoin({
      rows: this.api.getData(this.year, this.month),
      results: this.api.getResults(this.year, this.month)
    }).subscribe({
      next: ({ rows, results }) => {
        this.rows = rows;
        this.results = results;
        this.draft.clear();
        this.dirty.clear();
        for (const r of rows) this.draft.set(r.designation, { ...r.achievements });
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: err => this.fail(err, 'load Power & A/C data')
    });
  }

  value(designation: string, code: string): number | null {
    return this.draft.get(designation)?.[code] ?? null;
  }

  setValue(designation: string, code: string, raw: string): void {
    const entry = this.draft.get(designation);
    if (!entry) return;
    const trimmed = raw.trim();
    entry[code] = trimmed === '' ? null : Number(trimmed);
    this.dirty.add(designation);
  }

  isDirty(designation: string): boolean {
    return this.dirty.has(designation);
  }

  get unsavedCount(): number {
    return this.dirty.size;
  }

  label(designation: string): string {
    return this.rows.find(r => r.designation === designation)?.label ?? designation;
  }

  save(): void {
    const items: PowerAcSaveItem[] = [];
    for (const designation of this.dirty) {
      const entry = this.draft.get(designation);
      if (!entry) continue;
      for (const v of Object.values(entry)) {
        if (v !== null && (!Number.isFinite(v) || v < 0 || v > 100)) {
          this.error = 'Achievement % must be a number from 0 to 100.';
          this.cdr.detectChanges();
          return;
        }
      }
      // The allocation is not edited here, so it is sent back unchanged.
      const row = this.rows.find(r => r.designation === designation);
      items.push({ designation, achievements: { ...entry }, areaAllocation: row?.areaAllocation ?? null });
    }
    if (items.length === 0) {
      this.message = 'Nothing to save.';
      this.cdr.detectChanges();
      return;
    }
    this.busy = true;
    this.error = '';
    this.api.saveData(this.year, this.month, items).subscribe({
      next: r => {
        this.busy = false;
        this.message = `${r.saved} engineer group(s) saved for ${this.months[this.month - 1]} ${this.year}.`;
        this.load();
      },
      error: err => { this.busy = false; this.fail(err, 'save Power & A/C values'); }
    });
  }

  verify(): void {
    this.busy = true;
    this.error = '';
    this.api.verify(this.year, this.month).subscribe({
      next: r => { this.busy = false; this.message = r.message; this.load(); },
      error: err => { this.busy = false; this.fail(err, 'verify Power & A/C values'); }
    });
  }

  isVerified(designation: string): boolean {
    return this.rows.find(r => r.designation === designation)?.isVerified ?? false;
  }

  // Toggles verification of one engineer group's row (the ✓/✗ on the Preventive Maintenance row).
  toggleVerified(designation: string): void {
    this.busy = true;
    this.error = '';
    this.api.toggleVerified(this.year, this.month, designation).subscribe({
      next: r => {
        this.busy = false;
        const row = this.rows.find(x => x.designation === designation);
        if (row) row.isVerified = r.isVerified;
        this.cdr.detectChanges();
      },
      error: err => { this.busy = false; this.fail(err, 'change the verification'); }
    });
  }

  percent(value: number | null | undefined): string {
    return value === null || value === undefined ? '—' : `${(value * 100).toFixed(2)}%`;
  }

  achievementText(value: number | null | undefined): string {
    return value === null || value === undefined ? '—' : `${Number(value).toFixed(2)}%`;
  }

  statusClass(status: string | null | undefined): string {
    return status === 'Complete' ? 'status-complete' : 'status-incomplete';
  }

  private fail(err: HttpErrorResponse, action: string): void {
    this.loading = false;
    if (err.status === 401) this.error = 'Your session has expired. Sign in again.';
    else if (err.status === 403) this.error = `You are not authorized to ${action}.`;
    else if (err.status === 0) this.error = `Could not ${action}: the server is unreachable.`;
    else this.error = (err.error as { message?: string } | null)?.message ?? `Could not ${action} (HTTP ${err.status}).`;
    this.cdr.detectChanges();
  }
}
