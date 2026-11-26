/* File: power-ac-kpi.component.ts
   Power & A/C KPI page. One table (dbo.PowerAcPmData) holds every value: the Preventive Maintenance,
   Critical Safety, Corrective Action and Availability Achievement %, and the area allocation.
   The overview shows the calculated results. The two input grids save values and verify them. */
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { kpiScoreText, parseIncidentCount, safetyTargetText } from './power-ac-display';
import { FormsModule } from '@angular/forms';
import { catchError, forkJoin, of } from 'rxjs';
import { AuthService } from '../../../../services/auth.service';
import {
  PowerAcDataRow,
  PowerAcGroupResult,
  PowerAcKpiService,
  PowerAcMonthlyCell,
  PowerAcOverallStatus,
  PowerAcResults,
  PowerAcSaveItem
} from '../../../../services/power-ac-kpi.service';

type MonthlyField = 'scheduled' | 'attended';

interface MonthlyEntry {
  scheduled: number | null;
  attended: number | null;
  hasRow: boolean;
  isVerified: boolean;
}

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
  // Rule-scored KPIs: the grid enters the typed input, and the server calculates the score.
  readonly safetyCode = 'SAFETY';
  readonly availabilityCode = 'AVAILABILITY';
  readonly inputCodes = ['SAFETY', 'AVAILABILITY'];
  readonly safetyMaxIncidents = 1000;

  year = new Date().getFullYear();
  month = new Date().getMonth() + 1;
  rows: PowerAcDataRow[] = [];
  results: PowerAcResults | null = null;
  overallStatus: PowerAcOverallStatus | null = null;
  loading = false;
  busy = false;
  message = '';
  error = '';
  canEdit = false;
  canEditPm = false;
  canVerify = false;

  // Edited values per engineer group (designation) and KPI code, and the groups with unsaved edits.
  private draft = new Map<string, Record<string, number | null>>();
  private dirty = new Set<string>();
  // Typed inputs (incident count, actual availability %) per engineer group, and the ones the user edited.
  private inputDraft = new Map<string, Record<string, number | null>>();
  private inputTouched = new Set<string>();

  // Read-only monthly distribution and achievement counts, keyed by "month|designation".
  monthly = new Map<string, MonthlyEntry>();
  readonly monthNumbers = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];

  constructor(
    private readonly api: PowerAcKpiService,
    private readonly auth: AuthService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const normalize = (value: string) => value.toLowerCase().replace(/[^a-z0-9]/g, '');
    const isAssignedPlatformAdmin =
      normalize(this.auth.getRole() ?? '') === 'platformadmin' &&
      this.auth.getAssignedPages().some(page => normalize(page) === 'powerac');
    this.canEdit = isAssignedPlatformAdmin;
    this.canVerify = isAssignedPlatformAdmin;
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
      results: this.api.getResults(this.year, this.month),
      monthly: this.api.getMonthly(this.year),
      // The overall status is informational. If it cannot be loaded, the page still works.
      status: this.api.getOverallStatus(this.year).pipe(catchError(() => of(null))),
      permissions: this.api.getDataPermissions()
    }).subscribe({
      next: ({ rows, results, monthly, status, permissions }) => {
        this.rows = rows;
        this.results = results;
        this.overallStatus = status;
        this.canEditPm = this.canEdit || permissions.canEditPreventiveMaintenance;
        this.draft.clear();
        this.dirty.clear();
        this.inputDraft.clear();
        this.inputTouched.clear();
        for (const r of rows) {
          this.draft.set(r.designation, { ...r.achievements });
          this.inputDraft.set(r.designation, { ...(r.inputs ?? {}) });
        }
        this.monthly.clear();
        for (const c of monthly) this.monthly.set(this.monthlyKey(c.month, c.designation), { scheduled: c.scheduled, attended: c.attended, hasRow: c.hasRow, isVerified: c.isVerified });
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: err => this.fail(err, 'load Power & A/C data')
    });
  }

  value(designation: string, code: string): number | null {
    return this.draft.get(designation)?.[code] ?? null;
  }

  verificationStatusOf(designation: string): string {
    return this.rows.find(r => r.designation === designation)?.verificationStatus ?? 'Incomplete';
  }

  verificationMissingOf(designation: string): string[] {
    return this.rows.find(r => r.designation === designation)?.verificationMissing ?? [];
  }

  // Label shown to the user. "Verified" is only ever shown for a group that passes the current requirements.
  verificationLabel(designation: string): string {
    switch (this.verificationStatusOf(designation)) {
      case 'Verified': return 'Verified';
      case 'VerifiedNeedsReentry': return 'Verified – needs re-entry';
      case 'ReadyToVerify': return 'Ready to verify';
      default: return 'Incomplete';
    }
  }

  verificationTitle(designation: string): string {
    const missing = this.verificationMissingOf(designation);
    if (this.verificationStatusOf(designation) === 'VerifiedNeedsReentry') {
      return `Verified before the current requirements. Not counted as passing. Missing: ${missing.join(', ') || 'a legacy percentage must be re-entered'}. Click ✓ to unverify, then re-enter.`;
    }
    if (missing.length > 0) return `Missing: ${missing.join(', ')}.`;
    return this.isVerified(designation) ? 'Verified. Click to unverify.' : 'All required values are present. Click to verify.';
  }

  // True when the score for this KPI comes from an old percentage-only entry (no incident count / actual % yet).
  isLegacy(designation: string, code: string): boolean {
    return this.rows.find(r => r.designation === designation)?.legacyCodes?.includes(code) ?? false;
  }

  isLegacyResult(group: PowerAcGroupResult, code: string): boolean {
    return group.legacyCodes?.includes(code) ?? false;
  }

  legacyPercentText(designation: string, code: string): string {
    return this.achievementText(this.rows.find(r => r.designation === designation)?.achievements?.[code]);
  }

  // Number of engineer groups still on an old percentage for either rule-scored KPI, in the selected month.
  get legacyGroupCount(): number {
    return this.rows.filter(r => (r.legacyCodes ?? []).length > 0).length;
  }

  isInputCode(code: string): boolean {
    return this.inputCodes.includes(code);
  }

  inputValue(designation: string, code: string): number | null {
    return this.inputDraft.get(designation)?.[code] ?? null;
  }

  setInput(designation: string, code: string, raw: string): void {
    const entry = this.inputDraft.get(designation);
    if (!entry) return;
    if (code === this.safetyCode) {
      const parsed = parseIncidentCount(raw);
      if (!parsed.ok) {
        this.error = parsed.message;
        this.cdr.detectChanges();
        return;
      }
      entry[code] = parsed.value;
    } else {
      const trimmed = raw.trim();
      entry[code] = trimmed === '' ? null : Number(trimmed);
    }
    this.inputTouched.add(`${designation}|${code}`);
    this.dirty.add(designation);
  }

  // Target text for the overview: safety is a number of incidents, the others are percentages.
  targetText(c: { code: string; targetPercent: number }): string {
    return c.code === this.safetyCode ? safetyTargetText(c.targetPercent) : `${c.targetPercent}%`;
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
    if ((!this.canEdit && !this.canEditPm) || this.busy) return;
    const pmOnly = !this.canEdit;
    const items: PowerAcSaveItem[] = [];
    for (const designation of this.dirty) {
      const entry = this.draft.get(designation);
      if (!entry) continue;
      // Achievement % for the other KPIs (the rule-scored KPIs are excluded; they are sent as inputs).
      const achievements: Record<string, number | null> = {};
      for (const [code, v] of Object.entries(entry)) {
        if (this.isInputCode(code) || (pmOnly && code !== this.pmCode)) continue;
        if (v !== null && (!Number.isFinite(v) || v < 0 || v > 100)) {
          this.error = 'Achievement % must be a number from 0 to 100.';
          this.cdr.detectChanges();
          return;
        }
        achievements[code] = v;
      }

      // Only the inputs the user edited are sent, so untouched values (including older saved values) are not changed.
      const inputs: Record<string, number | null> = {};
      const inputDraft = this.inputDraft.get(designation) ?? {};
      for (const code of pmOnly ? [] : this.inputCodes) {
        if (!this.inputTouched.has(`${designation}|${code}`)) continue;
        const v = inputDraft[code] ?? null;
        if (v !== null) {
          if (code === this.safetyCode && (!Number.isInteger(v) || v < 0 || v > this.safetyMaxIncidents)) {
            this.error = `Critical Safety incidents must be a whole number from 0 to ${this.safetyMaxIncidents}.`;
            this.cdr.detectChanges();
            return;
          }
          if (code === this.availabilityCode && (!Number.isFinite(v) || v < 0 || v > 100)) {
            this.error = 'Power System Availability % must be a number from 0 to 100.';
            this.cdr.detectChanges();
            return;
          }
        }
        inputs[code] = v;
      }

      // The allocation is not edited here, so it is sent back unchanged.
      const row = this.rows.find(r => r.designation === designation);
      items.push({
        designation,
        achievements,
        inputs,
        areaAllocation: pmOnly ? null : row?.areaAllocation ?? null
      });
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
    if (!this.canVerify || this.busy) return;
    this.busy = true;
    this.error = '';
    this.api.verify(this.year, this.month).subscribe({
      next: r => {
        this.busy = false;
        this.load();   // load() clears the banners, so the result is shown after it starts
        this.message = r.message;
        if (r.issues.length > 0) this.error = r.issues.map(i => i.message).join(' ');
        this.cdr.detectChanges();
      },
      error: err => { this.busy = false; this.fail(err, 'verify Power & A/C values'); }
    });
  }

  private monthlyKey(month: number, designation: string): string {
    return `${month}|${designation}`;
  }

  monthlyValue(month: number, designation: string, field: MonthlyField): number | null {
    return this.monthly.get(this.monthlyKey(month, designation))?.[field] ?? null;
  }

  monthlyHasData(month: number, designation: string): boolean {
    const entry = this.monthly.get(this.monthlyKey(month, designation));
    return entry?.hasRow === true && (entry.scheduled !== null || entry.attended !== null);
  }

  monthlyVerified(month: number, designation: string): boolean {
    return this.monthly.get(this.monthlyKey(month, designation))?.isVerified ?? false;
  }

  // Toggles verification of one engineer group for one month. Same rule as the Preventive Maintenance ✓/✗.
  toggleMonthlyVerified(month: number, designation: string): void {
    if (!this.canVerify || this.busy) return;
    this.busy = true;
    this.error = '';
    this.api.toggleVerified(this.year, month, designation).subscribe({
      next: r => {
        this.busy = false;
        const entry = this.monthly.get(this.monthlyKey(month, designation));
        if (entry) entry.isVerified = r.isVerified;
        // The overview shows the selected month, so keep its ✓/✗ in step.
        if (month === this.month) {
          const row = this.rows.find(x => x.designation === designation);
          if (row) row.isVerified = r.isVerified;
        }
        this.cdr.detectChanges();
      },
      error: err => { this.busy = false; this.fail(err, 'change the verification'); }
    });
  }

  // Year total for one engineer group (sum of the months that have a value).
  monthlyTotal(designation: string, field: MonthlyField): number {
    let total = 0;
    for (const month of this.monthNumbers) total += this.monthlyValue(month, designation, field) ?? 0;
    return total;
  }

  isVerified(designation: string): boolean {
    return this.rows.find(r => r.designation === designation)?.isVerified ?? false;
  }

  // Toggles verification of one engineer group's row (the ✓/✗ on the Preventive Maintenance row).
  toggleVerified(designation: string): void {
    if (!this.canVerify || this.busy) return;
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
    return kpiScoreText(value);
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
