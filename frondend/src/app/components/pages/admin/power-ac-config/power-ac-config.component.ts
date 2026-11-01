/* File: power-ac-config.component.ts
   Description: Admin configuration for the Power & AC KPI: effective-dated component weights,
   component targets and scoring approval, calculation settings, engineer group mapping and
   engineer group user assignments. All changes require Admin or SuperAdmin on the server.
*/
import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { Region, RegionService } from '../../../../services/region.service';
import { User, UserService } from '../../../../services/user.service';
import {
  PowerAcComponent,
  PowerAcComponentVersion,
  PowerAcEffectiveWeights,
  PowerAcEngineerGroup,
  PowerAcGroupAssignment,
  PowerAcKpiService,
  PowerAcSetting,
  SavePowerAcSetting,
  sumWeights
} from '../../../../services/power-ac-kpi.service';

export type PowerAcConfigTab = 'weights' | 'components' | 'settings' | 'groups' | 'assignments';

interface WeightInput { componentId: number; code: string; name: string; weightPercent: number | null; }
interface ComponentEdit extends PowerAcComponent {
  saving: boolean;
  error: string;
  // Scoring version being created/updated by the form.
  effectiveYear?: number;
  effectiveMonth?: number;
  approvalNote?: string | null;
}
interface GroupEdit extends PowerAcEngineerGroup { saving: boolean; error: string; }

@Component({
  selector: 'app-power-ac-config',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './power-ac-config.component.html',
  // Same layout and table theme as the other KPI admin screens (Enterprise KPI, OTN, IP NW OP...).
  styleUrls: ['../enterprise-kpi/enterprise-kpi.component.scss', './power-ac-config.component.scss']
})
export class PowerAcConfigComponent implements OnInit {
  readonly months = Array.from({ length: 12 }, (_, i) => ({
    value: i + 1,
    label: new Date(2000, i, 1).toLocaleString('en', { month: 'long' })
  }));
  readonly years = Array.from({ length: 6 }, (_, i) => new Date().getFullYear() - 3 + i);
  readonly pointsMethods = ['TargetScaled', 'Linear'];

  tab: PowerAcConfigTab = 'weights';
  loading = false;
  message = '';
  error = '';

  // weights
  weightYear = new Date().getFullYear();
  weightMonth = new Date().getMonth() + 1;
  weightInputs: WeightInput[] = [];
  weightInfo: PowerAcEffectiveWeights | null = null;

  // components (table rows are read-only; Edit loads a copy into the form above the table)
  components: ComponentEdit[] = [];
  componentForm: ComponentEdit | null = null;

  // settings
  settings: PowerAcSetting[] = [];
  settingForm: SavePowerAcSetting = {
    effectiveYear: new Date().getFullYear(),
    effectiveMonth: new Date().getMonth() + 1,
    kpiDefinitionId: 2017,
    pointsMethod: 'TargetScaled',
    compositeTarget: 1,
    pointsMethodApproved: false,
    requireVerifiedData: true
  };

  // groups and assignments
  groups: GroupEdit[] = [];
  groupForm: GroupEdit | null = null;
  regions: Region[] = [];
  users: User[] = [];
  assignments: PowerAcGroupAssignment[] = [];
  newAssignment: { engineerGroupId: number | null; userId: number | null } = { engineerGroupId: null, userId: null };

  constructor(
    private readonly api: PowerAcKpiService,
    private readonly regionService: RegionService,
    private readonly userService: UserService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadWeights();
  }

  setTab(tab: PowerAcConfigTab): void {
    this.tab = tab;
    this.componentForm = null;
    this.groupForm = null;
    this.message = '';
    this.error = '';
    if (tab === 'weights') this.loadWeights();
    if (tab === 'components') this.loadComponents();
    if (tab === 'settings') this.loadSettings();
    if (tab === 'groups') this.loadGroups();
    if (tab === 'assignments') this.loadAssignments();
  }

  // ---------- weights ----------

  get weightTotal(): number {
    return sumWeights(this.weightInputs);
  }

  get weightsValid(): boolean {
    return this.weightInputs.length > 0
      && this.weightInputs.every(w => w.weightPercent !== null && Number(w.weightPercent) >= 0 && Number(w.weightPercent) <= 100)
      && Math.abs(this.weightTotal - 100) < 0.0001;
  }

  loadWeights(): void {
    this.begin();
    forkJoin({
      components: this.api.getComponents(this.weightYear, this.weightMonth),
      weights: this.api.getWeights(this.weightYear, this.weightMonth)
    }).subscribe({
      next: ({ components, weights }) => {
        this.weightInfo = weights;
        this.weightInputs = components.filter(c => c.isActive).map(c => ({
          componentId: c.id,
          code: c.code,
          name: c.name,
          weightPercent: weights.effective.find(w => w.componentId === c.id)?.weightPercent ?? null
        }));
        this.done();
      },
      error: err => this.fail(err, 'load weights')
    });
  }

  saveWeights(): void {
    if (!this.weightsValid) {
      this.error = `Weights must total 100%. They currently total ${this.weightTotal}%.`;
      return;
    }
    this.begin();
    this.api.saveWeights(this.weightYear, this.weightMonth,
      this.weightInputs.map(w => ({ componentId: w.componentId, weightPercent: Number(w.weightPercent) }))).subscribe({
      next: () => { this.done(`Weights saved, effective from ${this.months[this.weightMonth - 1].label} ${this.weightYear}.`); this.loadWeights(); },
      error: err => this.fail(err, 'save weights')
    });
  }

  // ---------- components ----------

  loadComponents(): void {
    this.begin();
    this.api.getComponents().subscribe({
      next: list => {
        this.components = list.map(c => ({ ...c, saving: false, error: '' }));
        this.loadVersions();
        this.done();
      },
      error: err => this.fail(err, 'load components')
    });
  }

  // Version history of every component, newest first.
  versions: PowerAcComponentVersion[] = [];

  loadVersions(): void {
    if (!this.components.length) { this.versions = []; return; }
    forkJoin(this.components.map(c => this.api.getComponentVersions(c.id))).subscribe({
      next: lists => {
        this.versions = lists.flat().sort((a, b) =>
          (b.effectiveYear * 100 + b.effectiveMonth) - (a.effectiveYear * 100 + a.effectiveMonth) || a.componentId - b.componentId);
        this.cdr.detectChanges();
      },
      error: err => { this.error = this.describe(err, 'load scoring versions'); this.cdr.detectChanges(); }
    });
  }

  editComponent(c: ComponentEdit): void {
    // A new version defaults to the current month; earlier months keep their own version.
    const now = new Date();
    this.componentForm = { ...c, saving: false, error: '', effectiveYear: now.getFullYear(), effectiveMonth: now.getMonth() + 1, approvalNote: '' };
    this.message = '';
  }

  cancelComponentEdit(): void {
    this.componentForm = null;
  }

  saveComponent(c: ComponentEdit): void {
    c.error = '';
    if (c.scoringMethod === 'Pending' && c.scoringApproved) {
      c.error = 'A Pending scoring method cannot be approved.';
      return;
    }
    if (c.scoringMethod === 'DeductionPerIncident' && (c.scoringParameter === null || c.scoringParameter === undefined)) {
      c.error = 'Enter the deduction percentage per incident.';
      return;
    }
    c.saving = true;
    const now = new Date();
    this.api.updateComponent(c.id, {
      effectiveYear: Number(c.effectiveYear ?? now.getFullYear()),
      effectiveMonth: Number(c.effectiveMonth ?? now.getMonth() + 1),
      approvalNote: c.approvalNote?.trim() || null,
      targetValue: Number(c.targetValue),
      scoringMethod: c.scoringMethod,
      scoringParameter: c.scoringParameter === null || (c.scoringParameter as unknown) === '' ? null : Number(c.scoringParameter),
      scoringApproved: c.scoringApproved,
      isActive: c.isActive,
      sortOrder: Number(c.sortOrder)
    }).subscribe({
      next: updated => {
        Object.assign(c, updated, { saving: false, error: '' });
        const row = this.components.find(x => x.id === c.id);
        if (row && row !== c) Object.assign(row, updated, { saving: false, error: '' });
        if (this.componentForm === c) this.componentForm = null;
        this.loadVersions();
        this.message = `${c.name} saved.`;
        this.cdr.detectChanges();
      },
      error: err => { c.saving = false; c.error = this.describe(err, 'save the component'); this.cdr.detectChanges(); }
    });
  }

  // ---------- settings ----------

  loadSettings(): void {
    this.begin();
    this.api.getSettings().subscribe({
      next: s => {
        this.settings = s;
        const latest = s[0];
        if (latest) {
          this.settingForm = {
            ...this.settingForm,
            kpiDefinitionId: latest.kpiDefinitionId,
            pointsMethod: latest.pointsMethod,
            compositeTarget: latest.compositeTarget,
            pointsMethodApproved: latest.pointsMethodApproved,
            requireVerifiedData: latest.requireVerifiedData
          };
        }
        this.done();
      },
      error: err => this.fail(err, 'load settings')
    });
  }

  saveSetting(): void {
    if (!(Number(this.settingForm.compositeTarget) > 0)) {
      this.error = 'The composite target must be greater than zero.';
      return;
    }
    this.begin();
    this.api.saveSetting({ ...this.settingForm, compositeTarget: Number(this.settingForm.compositeTarget), kpiDefinitionId: Number(this.settingForm.kpiDefinitionId) }).subscribe({
      next: () => { this.done('Settings saved.'); this.loadSettings(); },
      error: err => this.fail(err, 'save settings')
    });
  }

  // ---------- groups ----------

  loadGroups(): void {
    this.begin();
    forkJoin({ groups: this.api.getEngineerGroups(), regions: this.regionService.getAll() }).subscribe({
      next: ({ groups, regions }) => {
        this.groups = groups.map(g => ({ ...g, saving: false, error: '' }));
        this.regions = regions;
        this.done();
      },
      error: err => this.fail(err, 'load engineer groups')
    });
  }

  // The API returns camelCase (networkEngineer, leaCode); the shared Region interface uses lower case.
  regionLabel(r: Region): string {
    const raw = r as Region & { networkEngineer?: string; leaCode?: string };
    return `${raw.networkEngineer ?? raw.networkengineer} – ${raw.leaCode ?? raw.leacode} (id ${r.id})`;
  }

  editGroup(g: GroupEdit): void {
    this.groupForm = { ...g, saving: false, error: '' };
    this.message = '';
  }

  cancelGroupEdit(): void {
    this.groupForm = null;
  }

  groupRegionLabel(g: PowerAcEngineerGroup): string {
    const r = this.regions.find(x => x.id === g.regionDataId);
    return r ? this.regionLabel(r) : (g.networkEngineer ? `${g.networkEngineer} – ${g.leaCode ?? ''}` : '— not mapped —');
  }

  saveGroup(g: GroupEdit): void {
    g.error = '';
    if (g.mappingVerified && (!g.regionDataId || !g.pmDesignation?.trim())) {
      g.error = 'Set the regiondata record and the PowerAndAC designation before verifying.';
      return;
    }
    g.saving = true;
    this.api.updateEngineerGroup(g.id, {
      regionDataId: g.regionDataId ? Number(g.regionDataId) : null,
      pmDesignation: g.pmDesignation?.trim() || null,
      mappingVerified: g.mappingVerified,
      mappingNote: g.mappingNote?.trim() || null,
      isActive: g.isActive
    }).subscribe({
      next: updated => {
        Object.assign(g, updated, { saving: false, error: '' });
        const row = this.groups.find(x => x.id === g.id);
        if (row && row !== g) Object.assign(row, updated, { saving: false, error: '' });
        if (this.groupForm === g) this.groupForm = null;
        this.message = `${g.code} mapping saved.`;
        this.cdr.detectChanges();
      },
      error: err => { g.saving = false; g.error = this.describe(err, 'save the mapping'); this.cdr.detectChanges(); }
    });
  }

  // ---------- assignments ----------

  loadAssignments(): void {
    this.begin();
    forkJoin({ assignments: this.api.getAssignments(), groups: this.api.getEngineerGroups(), users: this.userService.getAllUsers() }).subscribe({
      next: ({ assignments, groups, users }) => {
        this.assignments = assignments;
        this.groups = groups.map(g => ({ ...g, saving: false, error: '' }));
        this.users = users.filter(u => u.isActive);
        this.done();
      },
      error: err => this.fail(err, 'load assignments')
    });
  }

  addAssignment(): void {
    if (!this.newAssignment.engineerGroupId || !this.newAssignment.userId) {
      this.error = 'Choose an engineer group and a user.';
      return;
    }
    this.begin();
    this.api.createAssignment(Number(this.newAssignment.engineerGroupId), Number(this.newAssignment.userId)).subscribe({
      next: () => { this.newAssignment = { engineerGroupId: null, userId: null }; this.done('Assignment added.'); this.loadAssignments(); },
      error: err => this.fail(err, 'add the assignment')
    });
  }

  removeAssignment(a: PowerAcGroupAssignment): void {
    if (!confirm(`Remove ${a.userName ?? a.serviceId} from ${a.engineerGroupCode}?`)) return;
    this.begin();
    this.api.deleteAssignment(a.id).subscribe({
      next: () => { this.done('Assignment removed.'); this.loadAssignments(); },
      error: err => this.fail(err, 'remove the assignment')
    });
  }

  // ---------- helpers ----------

  private begin(): void { this.loading = true; this.error = ''; }

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
