/* File: power-ac-kpi.component.ts
   Description: Four-component Power & AC KPI screen, laid out like the Tower Maintenance and
   Routine Maintenance platform pages: a KPI overview table (KPI rows x engineer columns) followed by
   one "distribution vs achievement" card per component (month rows x engineer column pairs).
   Tabs: Results (all viewers), Area Allocation and Component Data (assigned engineers / Admins),
   Review (verifiers). The server enforces every permission; the UI only hides what the user cannot do.
*/
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../../../services/auth.service';
import { PowerAndACRecord, PowerAndACService } from '../../../../services/power-and-ac.service';
import {
  isValidAllocation,
  PowerAcAllocation,
  PowerAcComponent,
  PowerAcComponentRecord,
  PowerAcEngineerGroup,
  PowerAcGroupResult,
  PowerAcKpiResults,
  PowerAcAuditEntry,
  PowerAcKpiService,
  PowerAcPeriodType,
  PowerAcReadinessReport,
  PowerAcReadinessState
} from '../../../../services/power-ac-kpi.service';
import { formatEngineerDisplay } from '../../../../utils/region-display.utils';

export type PowerAcTab = 'results' | 'allocations' | 'data' | 'review';

// One row of the readiness table (a check per engineer group).
interface ReadinessRow {
  key: string;
  label: string;
}

interface DataRow {
  group: PowerAcEngineerGroup;
  record: PowerAcComponentRecord | null;
  applicableCount: number | null;
  completedCount: number | null;
  incidentCount: number | null;
  achievementPercent: number | null;
  remarks: string;
  error: string;
  saving: boolean;
}

interface AllocationRow extends PowerAcAllocation {
  input: number | string | null;
  remarksInput: string;
  error: string;
}

// One row of the KPI overview table.
interface OverviewRow {
  key: string;
  label: string;
  target: string;
  weight: string;
  emphasis?: boolean;
}

@Component({
  selector: 'app-power-ac-kpi',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './power-ac-kpi.component.html',
  // Same theme as the Tower Maintenance / Routine Maintenance platform pages.
  styleUrls: ['../tm-activity-plan/tm-activity-plan.component.scss', './power-ac-kpi.component.scss']
})
export class PowerAcKpiComponent implements OnInit {
  readonly months = Array.from({ length: 12 }, (_, i) => ({
    value: i + 1,
    label: new Date(2000, i, 1).toLocaleString('en', { month: 'long' })
  }));
  readonly quarters = [1, 2, 3, 4].map(q => ({ value: q, label: `Q${q}` }));
  readonly years = Array.from({ length: 6 }, (_, i) => new Date().getFullYear() - 3 + i);
  readonly componentCodes = ['PM', 'SAFETY', 'CORRECTIVE', 'AVAILABILITY'];

  tab: PowerAcTab = 'results';
  year = new Date().getFullYear();
  month = new Date().getMonth() + 1;
  periodType: PowerAcPeriodType = 'Month';
  quarter = Math.floor(new Date().getMonth() / 3) + 1;

  canCalculate = false;
  canVerify = false;
  canVerifyPm = false;
  isAdmin = false;

  loading = false;
  message = '';
  error = '';

  results: PowerAcKpiResults | null = null;
  readiness: PowerAcReadinessReport | null = null;
  history: Record<number, PowerAcAuditEntry[] | undefined> = {};

  readonly readinessStateLabels: Record<PowerAcReadinessState, string> = {
    NotConfigured: 'Not configured',
    DataIncomplete: 'Data incomplete',
    AwaitingVerification: 'Awaiting verification',
    AwaitingApproval: 'Awaiting approval',
    ScoreReady: 'Score-ready'
  };
  readonly readinessStates: PowerAcReadinessState[] = ['NotConfigured', 'DataIncomplete', 'AwaitingVerification', 'AwaitingApproval', 'ScoreReady'];
  pmRecords: PowerAndACRecord[] = [];
  componentRecords: PowerAcComponentRecord[] = [];

  components: PowerAcComponent[] = [];
  groups: PowerAcEngineerGroup[] = [];

  allocationRows: AllocationRow[] = [];
  allocationTotal = 0;
  allocationMissing = 0;
  allocationComplete = false;

  dataComponentId: number | null = null;
  dataRows: DataRow[] = [];

  reviewRecords: PowerAcComponentRecord[] = [];
  reviewComments: Record<number, string> = {};

  constructor(
    private readonly api: PowerAcKpiService,
    private readonly pmApi: PowerAndACService,
    private readonly auth: AuthService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    const role = (this.auth.getRole() ?? '').replace(/\s+/g, '').toLowerCase();
    const pageEditor = this.auth.canEditPage('Power & A/C');
    this.isAdmin = role === 'admin' || role === 'superadmin';
    this.canCalculate = this.isAdmin || pageEditor;
    this.canVerify = role === 'superadmin' || (role === 'platformadmin' && pageEditor);
    // Same rule as the existing Power & A/C page's verify toggle.
    this.canVerifyPm = pageEditor;

    forkJoin({ components: this.api.getComponents(this.year, this.month), groups: this.api.getEngineerGroups() })
      .subscribe({
        next: ({ components, groups }) => {
          this.components = components;
          this.groups = groups.filter(g => g.isActive);
          this.dataComponentId = this.entryComponents[0]?.id ?? null;
          this.loadResults();
        },
        error: err => this.fail(err, 'load the Power & AC configuration')
      });
  }

  // ---------- derived ----------

  get period(): number {
    return this.periodType === 'Month' ? this.month : this.quarter;
  }

  get periodLabel(): string {
    return this.periodType === 'Month'
      ? `${this.months[this.month - 1].label} ${this.year}`
      : `Q${this.quarter} ${this.year}`;
  }

  // Last month of the selected period; detail tables show the year up to this month.
  get periodEndMonth(): number {
    return this.periodType === 'Month' ? this.month : this.quarter * 3;
  }

  get detailMonths(): number[] {
    return Array.from({ length: this.periodEndMonth }, (_, i) => i + 1);
  }

  get manageableGroups(): PowerAcEngineerGroup[] {
    return this.groups.filter(g => g.canManage);
  }

  get canEnterData(): boolean {
    return this.manageableGroups.length > 0;
  }

  // Components entered on this screen (PM keeps using the existing PowerAndAC screen).
  get entryComponents(): PowerAcComponent[] {
    return this.components.filter(c => c.dataSource === 'ComponentResult' && c.isActive);
  }

  get dataComponent(): PowerAcComponent | undefined {
    return this.components.find(c => c.id === this.dataComponentId);
  }

  get isIncidentComponent(): boolean {
    return this.dataComponent?.code === 'SAFETY';
  }

  get allocationEdits(): AllocationRow[] {
    return this.allocationRows.filter(r => r.canEdit && r.input !== null && r.input !== '' && Number(r.input) !== r.allocationValue);
  }

  get overviewRows(): OverviewRow[] {
    const rows: OverviewRow[] = (this.results?.components ?? [])
      .filter(c => c.isActive)
      .map(c => ({
        key: c.code,
        label: c.name,
        target: c.targetUnit === '%' ? `${c.targetValue}%` : `${c.targetValue} ${c.targetUnit.toLowerCase()}`,
        weight: c.currentWeightPercent === null ? '—' : `${c.currentWeightPercent}%`
      }));
    rows.push(
      { key: 'COMPOSITE', label: 'Power & A/C KPI (weighted)', target: this.results ? `${this.results.compositeTarget * 100}%` : '—', weight: `${this.results?.weightTotal ?? 0}%`, emphasis: true },
      { key: 'ALLOCATION', label: 'Area Allocation', target: '—', weight: '—' },
      { key: 'MAX', label: 'Maximum Points', target: '—', weight: '—' },
      { key: 'POINTS', label: 'Points Achieved', target: '—', weight: '—', emphasis: true },
      { key: 'STATUS', label: 'Status', target: '—', weight: '—' }
    );
    return rows;
  }

  get groupsWithNotes(): PowerAcGroupResult[] {
    return (this.results?.groups ?? []).filter(g => g.issues.length || g.warnings.length);
  }

  setTab(tab: PowerAcTab): void {
    this.tab = tab;
    this.message = '';
    this.error = '';
    if (tab === 'results') this.loadResults();
    if (tab === 'allocations') this.loadAllocations();
    if (tab === 'data') this.loadDataRows();
    if (tab === 'review') this.loadReview();
  }

  onPeriodChange(): void {
    this.setTab(this.tab);
  }

  // ---------- header helpers ----------

  groupHeader(g: { networkEngineer?: string | null; engineerName?: string | null; engineerGroupCode?: string; code?: string }): string {
    const code = g.networkEngineer || g.engineerGroupCode || g.code || '';
    return formatEngineerDisplay(code, g.engineerName);
  }

  groupHeaderById(engineerGroupId: number): string {
    const g = this.groups.find(x => x.id === engineerGroupId);
    return g ? this.groupHeader(g) : String(engineerGroupId);
  }

  areaOf(engineerGroupId: number): string {
    return this.groups.find(x => x.id === engineerGroupId)?.leaCode ?? '';
  }

  // ---------- results ----------

  loadResults(): void {
    this.begin();
    forkJoin({
      saved: this.api.getResults(this.year, this.periodType, this.period),
      pm: this.pmApi.getByYear(this.year),
      records: this.api.getComponentRecords({ year: this.year }),
      readiness: this.api.getReadiness(this.year, this.periodType, this.period)
    }).subscribe({
      next: ({ saved, pm, records, readiness }) => {
        this.pmRecords = pm;
        this.componentRecords = records;
        this.readiness = readiness;
        if (saved.groups.length > 0) {
          this.results = saved;
          this.done();
        } else {
          // Nothing stored yet: show the live preview so users still see status and gaps.
          this.api.preview(this.year, this.periodType, this.period).subscribe({
            next: preview => { this.results = preview; this.done(); },
            error: err => this.fail(err, 'calculate the preview')
          });
        }
      },
      error: err => this.fail(err, 'load results')
    });
  }

  refreshPreview(): void {
    this.begin();
    forkJoin({
      preview: this.api.preview(this.year, this.periodType, this.period),
      readiness: this.api.getReadiness(this.year, this.periodType, this.period)
    }).subscribe({
      next: ({ preview, readiness }) => { this.results = preview; this.readiness = readiness; this.done(); },
      error: err => this.fail(err, 'calculate the preview')
    });
  }

  calculateAndSave(): void {
    if (!this.canCalculate) return;
    this.begin();
    this.api.calculate(this.year, this.periodType, this.period).subscribe({
      next: r => { this.results = r; this.done(`Results for ${this.periodLabel} were calculated and saved.`); },
      error: err => this.fail(err, 'calculate and save results')
    });
  }

  // ---------- readiness ----------

  get readinessRows(): ReadinessRow[] {
    const rows: ReadinessRow[] = [
      { key: 'MAPPING', label: 'Mapping verified' },
      { key: 'ALLOCATION', label: 'Allocation entered' }
    ];
    for (const c of this.readiness?.groups[0]?.components ?? []) {
      const name = this.components.find(x => x.id === c.componentId)?.name ?? c.code;
      rows.push({ key: `DATA:${c.code}`, label: `${name} – data complete` });
      rows.push({ key: `VERIFIED:${c.code}`, label: `${name} – verified` });
    }
    rows.push({ key: 'APPROVED', label: 'Scoring methods approved' });
    return rows;
  }

  readinessGroup(engineerGroupId: number) {
    return this.readiness?.groups.find(g => g.engineerGroupId === engineerGroupId);
  }

  readinessCheck(row: ReadinessRow, engineerGroupId: number): boolean | null {
    const g = this.readinessGroup(engineerGroupId);
    if (!g) return null;
    if (row.key === 'MAPPING') return g.mappingVerified;
    if (row.key === 'ALLOCATION') return g.allocationEntered;
    if (row.key === 'APPROVED') return g.components.every(c => c.approved) && !!this.readiness?.pointsMethodApproved;
    const [kind, code] = row.key.split(':');
    const c = g.components.find(x => x.code === code);
    if (!c) return null;
    return kind === 'DATA' ? c.dataComplete : c.verified;
  }

  readinessTitle(row: ReadinessRow, engineerGroupId: number): string {
    const g = this.readinessGroup(engineerGroupId);
    if (!g) return '';
    const [kind, code] = row.key.split(':');
    if (kind === 'DATA') return g.components.find(x => x.code === code)?.missing.join('\n') ?? '';
    return '';
  }

  readinessClass(state: string | undefined): string {
    switch (state) {
      case 'ScoreReady': return 'status-complete';
      case 'AwaitingApproval': case 'AwaitingVerification': return 'status-pending';
      default: return 'status-incomplete';
    }
  }

  // ---------- record history ----------

  toggleHistory(row: DataRow): void {
    if (!row.record) return;
    const id = row.record.id;
    if (this.history[id]) { this.history[id] = undefined; return; }
    this.api.getRecordHistory(id).subscribe({
      next: entries => { this.history[id] = entries; this.cdr.detectChanges(); },
      error: err => { row.error = this.describe(err, 'load the history'); this.cdr.detectChanges(); }
    });
  }

  componentScore(group: PowerAcGroupResult, code: string) {
    return group.components.find(c => c.code === code);
  }

  // Text for one overview cell (KPI row x engineer column).
  overviewValue(row: OverviewRow, g: PowerAcGroupResult): string {
    switch (row.key) {
      case 'COMPOSITE': return this.percent(g.compositeAchievement, 2);
      case 'ALLOCATION': return this.number(g.allocationValue);
      case 'MAX': return this.number(g.maximumPoints);
      case 'POINTS': return this.number(g.pointsAchieved);
      case 'STATUS': return g.status;
      default: {
        const c = this.componentScore(g, row.key);
        if (!c) return '—';
        return c.status === 'Complete' ? this.percent(c.score) : c.status;
      }
    }
  }

  // Approval icon state for component rows: true = scored, false = not scored, null = no icon.
  overviewApproved(row: OverviewRow, g: PowerAcGroupResult): boolean | null {
    if (!this.componentCodes.includes(row.key)) return null;
    const c = this.componentScore(g, row.key);
    return c ? c.status === 'Complete' : null;
  }

  overviewTitle(row: OverviewRow, g: PowerAcGroupResult): string {
    if (row.key === 'STATUS') return g.issues.join('\n') || 'Complete';
    const c = this.componentScore(g, row.key);
    return c?.message ?? '';
  }

  statusClass(status: string | null | undefined): string {
    switch (status) {
      case 'Complete': case 'Verified': return 'status-complete';
      case 'Pending': case 'Submitted': return 'status-pending';
      case 'Draft': return 'status-draft';
      default: return 'status-incomplete';
    }
  }

  // ---------- detail tables ----------

  pmRecord(g: PowerAcEngineerGroup, month: number): PowerAndACRecord | undefined {
    return g.pmDesignation
      ? this.pmRecords.find(r => r.designation === g.pmDesignation && r.month === month)
      : undefined;
  }

  componentRecord(g: PowerAcEngineerGroup, code: string, month: number): PowerAcComponentRecord | undefined {
    const component = this.components.find(c => c.code === code);
    return component
      ? this.componentRecords.find(r => r.engineerGroupId === g.id && r.componentId === component.id && r.month === month)
      : undefined;
  }

  allocationFor(g: PowerAcEngineerGroup): string {
    const result = this.results?.groups.find(x => x.engineerGroupId === g.id);
    return this.number(result?.allocationValue ?? null);
  }

  // Applicable / completed (or entered %) text for the Corrective and Availability tables.
  completedText(record: PowerAcComponentRecord | undefined): string {
    if (!record) return '—';
    if (record.completedCount !== null) return this.number(record.completedCount, 0);
    if (record.achievementPercent !== null) return `${record.achievementPercent}%`;
    return '—';
  }

  togglePmVerified(record: PowerAndACRecord | undefined): void {
    if (!record || !this.canVerifyPm) return;
    this.pmApi.toggleVerified(record.id).subscribe({
      next: result => { record.isVerified = result.isVerified; this.cdr.detectChanges(); },
      error: err => { this.error = this.describe(err, 'change the verification'); this.cdr.detectChanges(); }
    });
  }

  percent(value: number | null | undefined, digits = 1): string {
    return value === null || value === undefined ? '—' : `${(value * 100).toFixed(digits)}%`;
  }

  number(value: number | null | undefined, digits = 2): string {
    return value === null || value === undefined ? '—' : Number(value).toFixed(digits);
  }

  // ---------- allocations ----------

  loadAllocations(): void {
    this.begin();
    this.api.getAllocations(this.year, this.month).subscribe({
      next: summary => {
        this.allocationRows = summary.items.map(i => ({
          ...i, input: i.allocationValue, remarksInput: i.remarks ?? '', error: ''
        }));
        this.allocationTotal = summary.total;
        this.allocationMissing = summary.missingCount;
        this.allocationComplete = summary.isComplete;
        this.done();
      },
      error: err => this.fail(err, 'load allocations')
    });
  }

  get draftAllocationTotal(): number {
    return this.allocationRows.reduce((sum, r) => sum + (isValidAllocation(r.input) ? Number(r.input) : 0), 0);
  }

  validateAllocation(row: AllocationRow): void {
    row.error = row.input === null || row.input === '' || isValidAllocation(row.input)
      ? ''
      : 'Enter a number from 0 to 1,000,000 with at most 4 decimal places.';
  }

  saveAllocations(): void {
    this.allocationRows.forEach(r => this.validateAllocation(r));
    const edits = this.allocationEdits;
    if (this.allocationRows.some(r => r.error)) {
      this.error = 'Fix the highlighted allocation values before saving.';
      return;
    }
    if (edits.length === 0) {
      this.message = 'There are no allocation changes to save.';
      return;
    }
    this.begin();
    this.api.saveAllocations(this.year, this.month, edits.map(r => ({
      engineerGroupId: r.engineerGroupId,
      allocationValue: Number(r.input),
      remarks: r.remarksInput?.trim() || null
    }))).subscribe({
      next: () => { this.done(`Saved ${edits.length} allocation value(s), effective from ${this.months[this.month - 1].label} ${this.year}.`); this.loadAllocations(); },
      error: err => this.fail(err, 'save allocations')
    });
  }

  // ---------- component data ----------

  loadDataRows(): void {
    if (!this.dataComponentId) { this.dataRows = []; return; }
    this.begin();
    this.api.getComponentRecords({ year: this.year, month: this.month, componentId: this.dataComponentId }).subscribe({
      next: records => {
        this.dataRows = this.manageableGroups.map(group => {
          const record = records.find(r => r.engineerGroupId === group.id) ?? null;
          return {
            group, record,
            applicableCount: record?.applicableCount ?? null,
            completedCount: record?.completedCount ?? null,
            incidentCount: record?.incidentCount ?? null,
            achievementPercent: record?.achievementPercent ?? null,
            remarks: record?.remarks ?? '',
            error: '',
            saving: false
          };
        });
        this.done();
      },
      error: err => this.fail(err, 'load component data')
    });
  }

  rowEditable(row: DataRow): boolean {
    return !row.record || row.record.canEdit;
  }

  validateRow(row: DataRow): string {
    const nonNegative = (v: number | null) => v === null || (Number.isFinite(Number(v)) && Number(v) >= 0);
    if (this.isIncidentComponent) {
      if (row.incidentCount === null) return 'Enter the incident count (0 if there were none).';
      if (!Number.isInteger(Number(row.incidentCount)) || Number(row.incidentCount) < 0) return 'Incident count must be a whole number of 0 or more.';
      return '';
    }
    if (!nonNegative(row.applicableCount) || !nonNegative(row.completedCount)) return 'Counts cannot be negative.';
    if ((row.applicableCount === null) !== (row.completedCount === null)) return 'Enter applicable and completed counts together.';
    if (row.achievementPercent !== null && (Number(row.achievementPercent) < 0 || Number(row.achievementPercent) > 100)) return 'Percentage must be between 0 and 100.';
    if (row.applicableCount === null && row.achievementPercent === null) return 'Enter counts or a percentage.';
    return '';
  }

  saveRow(row: DataRow): void {
    row.error = this.validateRow(row);
    if (row.error || !this.dataComponentId) return;
    row.saving = true;
    this.api.saveComponentRecord({
      engineerGroupId: row.group.id,
      year: this.year,
      month: this.month,
      componentId: this.dataComponentId,
      applicableCount: this.isIncidentComponent ? null : this.toNumber(row.applicableCount),
      completedCount: this.isIncidentComponent ? null : this.toNumber(row.completedCount),
      incidentCount: this.isIncidentComponent ? this.toNumber(row.incidentCount) : null,
      achievementPercent: this.isIncidentComponent ? null : this.toNumber(row.achievementPercent),
      remarks: row.remarks.trim() || null
    }).subscribe({
      next: record => { row.record = record; row.saving = false; this.message = `${row.group.code} saved as Draft.`; this.cdr.detectChanges(); },
      error: err => { row.saving = false; row.error = this.describe(err, 'save'); this.cdr.detectChanges(); }
    });
  }

  submitRow(row: DataRow): void {
    if (!row.record) return;
    row.saving = true;
    this.api.submitComponentRecord(row.record.id).subscribe({
      next: record => { row.record = { ...record, canEdit: false }; row.saving = false; this.message = `${row.group.code} submitted for verification.`; this.cdr.detectChanges(); },
      error: err => { row.saving = false; row.error = this.describe(err, 'submit'); this.cdr.detectChanges(); }
    });
  }

  // ---------- review ----------

  loadReview(): void {
    if (!this.canVerify) return;
    this.begin();
    this.api.getComponentRecords({ year: this.year, month: this.month, status: 'Submitted' }).subscribe({
      next: records => { this.reviewRecords = records; this.done(); },
      error: err => this.fail(err, 'load submitted records')
    });
  }

  review(record: PowerAcComponentRecord, approve: boolean): void {
    const comment = (this.reviewComments[record.id] ?? '').trim();
    if (!approve && !comment) {
      this.error = 'Enter a comment explaining why the record is rejected.';
      return;
    }
    this.begin();
    this.api.verifyComponentRecord(record.id, approve, comment || null).subscribe({
      next: () => { this.done(`${record.engineerGroupCode} ${record.componentCode} ${approve ? 'verified' : 'rejected'}.`); this.loadReview(); },
      error: err => this.fail(err, approve ? 'verify the record' : 'reject the record')
    });
  }

  componentName(id: number): string {
    return this.components.find(c => c.id === id)?.name ?? String(id);
  }

  // ---------- helpers ----------

  private toNumber(value: number | string | null): number | null {
    return value === null || value === '' ? null : Number(value);
  }

  private begin(): void {
    this.loading = true;
    this.error = '';
  }

  private done(message = ''): void {
    this.loading = false;
    if (message) this.message = message;
    this.cdr.detectChanges();
  }

  private fail(err: HttpErrorResponse, action: string): void {
    this.loading = false;
    this.error = this.describe(err, action);
    this.cdr.detectChanges();
  }

  private describe(err: HttpErrorResponse, action: string): string {
    if (err.status === 0) return `Could not ${action}: the server is unreachable.`;
    if (err.status === 401) return 'Your session has expired. Sign in again.';
    if (err.status === 403) return `You are not authorized to ${action}.`;
    const body = err.error as { message?: string; title?: string; errors?: Record<string, string[]> } | null;
    if (body?.message) return body.message;
    if (body?.errors) return Object.values(body.errors).flat().join(' ');
    return body?.title ?? `Could not ${action} (HTTP ${err.status}).`;
  }
}
