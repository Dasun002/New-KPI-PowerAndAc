import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { PowerAcKpiComponent } from './power-ac-kpi.component';
import {
  isValidAllocation,
  PowerAcComponent,
  PowerAcEngineerGroup,
  PowerAcKpiResults,
  PowerAcKpiService,
  sumWeights
} from '../../../../services/power-ac-kpi.service';
import { AuthService } from '../../../../services/auth.service';
import { PowerAndACService } from '../../../../services/power-and-ac.service';

const components: PowerAcComponent[] = [
  { id: 1, code: 'PM', name: 'Preventive Maintenance', targetValue: 100, targetUnit: '%', dataSource: 'PowerAndAC', scoringMethod: 'RatioCapped', scoringParameter: null, scoringApproved: true, sortOrder: 1, isActive: true, currentWeightPercent: 25, allowedScoringMethods: [] },
  { id: 2, code: 'SAFETY', name: 'Critical Safety', targetValue: 0, targetUnit: 'Incidents', dataSource: 'ComponentResult', scoringMethod: 'Pending', scoringParameter: null, scoringApproved: false, sortOrder: 2, isActive: true, currentWeightPercent: 25, allowedScoringMethods: [] },
  { id: 3, code: 'CORRECTIVE', name: 'Corrective Action', targetValue: 100, targetUnit: '%', dataSource: 'ComponentResult', scoringMethod: 'RatioCapped', scoringParameter: null, scoringApproved: true, sortOrder: 3, isActive: true, currentWeightPercent: 25, allowedScoringMethods: [] },
  { id: 4, code: 'AVAILABILITY', name: 'Power System Availability', targetValue: 99, targetUnit: '%', dataSource: 'ComponentResult', scoringMethod: 'DirectPercent', scoringParameter: null, scoringApproved: true, sortOrder: 4, isActive: true, currentWeightPercent: 25, allowedScoringMethods: [] }
];

const groups: PowerAcEngineerGroup[] = [
  { id: 1, code: 'WPC-1', workbookLabel: 'E/NW/WPC1', workbookAllocationCell: 'D31', regionDataId: 1, leaCode: 'HK', mappingVerified: true, sortOrder: 1, isActive: true, canManage: true },
  { id: 2, code: 'WPC-2', workbookLabel: 'E/NW/WPC2', workbookAllocationCell: 'D32', regionDataId: 2, leaCode: 'CEN/MD', mappingVerified: true, sortOrder: 2, isActive: true, canManage: false }
];

const score = (code: string, status: 'Complete' | 'Pending' | 'Incomplete', value: number | null) => ({
  componentId: 1, code, name: code, weightPercent: 25, scoringMethod: 'RatioCapped', applicableCount: null, completedCount: null,
  incidentCount: null, achievementRatio: value, score: value, weightedScore: value === null ? null : value / 4, status, message: status === 'Pending' ? 'Scoring formula is pending business confirmation.' : null
});

const results: PowerAcKpiResults = {
  year: 2026, periodType: 'Month', periodNumber: 3, isPreview: false, calculatedAt: '2026-11-02T10:00:00Z', calculatedBy: 'admin',
  pointsMethod: 'TargetScaled', compositeTarget: 1, pointsMethodApproved: true, requireVerifiedData: true,
  pointsApplicable: 5000, totalAllocation: 10, allocationsComplete: true, weightsValid: true, weightTotal: 100,
  globalIssues: ['Area allocation is missing for 1 engineer group(s): EP.'],
  components,
  groups: [
    {
      engineerGroupId: 1, engineerGroupCode: 'WPC-1', workbookLabel: 'E/NW/WPC1', leaCode: 'HK', status: 'Complete',
      compositeAchievement: 0.925, pointsApplicable: 5000, allocationValue: 7, totalAllocation: 10, maximumPoints: 3500, pointsAchieved: 3237.5,
      issues: [], warnings: [],
      components: [score('PM', 'Complete', 0.9), score('SAFETY', 'Complete', 1), score('CORRECTIVE', 'Complete', 0.8), score('AVAILABILITY', 'Complete', 1)]
    },
    {
      engineerGroupId: 2, engineerGroupCode: 'WPC-2', workbookLabel: 'E/NW/WPC2', leaCode: 'CEN/MD', status: 'Pending',
      compositeAchievement: null, pointsApplicable: 5000, allocationValue: 3, totalAllocation: 10, maximumPoints: 1500, pointsAchieved: null,
      issues: ['SAFETY: Scoring formula is pending business confirmation.'], warnings: [],
      components: [score('PM', 'Complete', 0.5), score('SAFETY', 'Pending', null), score('CORRECTIVE', 'Complete', 0.8), score('AVAILABILITY', 'Complete', 1)]
    }
  ],
  completeCount: 1, incompleteCount: 0, pendingCount: 1, totalPointsAchieved: 3237.5, totalMaximumPoints: 5000
};

const readinessComponent = (code: string, dataComplete: boolean, verified: boolean, approved: boolean) => ({
  componentId: 1, code, hasData: dataComplete, dataComplete, verified, approved, scoringMethod: 'RatioCapped', missing: dataComplete ? [] : ['April data is missing.']
});

const readiness = {
  settingsConfigured: true, pointsMethodApproved: true, requireVerifiedData: true, weightsValid: true, weightTotal: 100,
  allocationsEntered: 1, groupCount: 2, allocationsComplete: false, approvedComponentCount: 3, componentCount: 4,
  pendingBusinessDecisions: ['Critical Safety: scoring rule not yet provided by the business.', 'Engineer group EP (D19): mapping needs business confirmation.'],
  groups: [
    { engineerGroupId: 1, code: 'WPC-1', mappingVerified: true, allocationEntered: true, state: 'AwaitingApproval', resultStatus: 'Pending',
      components: [readinessComponent('PM', true, true, true), readinessComponent('SAFETY', true, true, false), readinessComponent('CORRECTIVE', true, true, true), readinessComponent('AVAILABILITY', true, true, true)],
      blockers: ['SAFETY: scoring method is not approved.'] },
    { engineerGroupId: 2, code: 'WPC-2', mappingVerified: true, allocationEntered: false, state: 'NotConfigured', resultStatus: 'Incomplete',
      components: [readinessComponent('PM', false, false, true), readinessComponent('SAFETY', false, false, false), readinessComponent('CORRECTIVE', true, false, true), readinessComponent('AVAILABILITY', true, true, true)],
      blockers: ['Area allocation is not entered.'] }
  ],
  stateCounts: { NotConfigured: 1, DataIncomplete: 0, AwaitingVerification: 0, AwaitingApproval: 1, ScoreReady: 0 }
};

describe('PowerAcKpiComponent', () => {
  function apiStub() {
    return {
      getComponents: vi.fn(() => of(components)),
      getEngineerGroups: vi.fn(() => of(groups)),
      getResults: vi.fn(() => of(results)),
      preview: vi.fn(() => of({ ...results, isPreview: true })),
      calculate: vi.fn(() => of(results)),
      getAllocations: vi.fn(() => of({
        year: 2026, month: 3, total: 7, missingCount: 1, isComplete: false,
        items: [
          { engineerGroupId: 1, engineerGroupCode: 'WPC-1', workbookLabel: 'E/NW/WPC1', workbookAllocationCell: 'D31', mappingVerified: true, allocationValue: 7, canEdit: true },
          { engineerGroupId: 2, engineerGroupCode: 'WPC-2', workbookLabel: 'E/NW/WPC2', workbookAllocationCell: 'D32', mappingVerified: true, allocationValue: null, canEdit: false }
        ]
      })),
      saveAllocations: vi.fn(() => of({})),
      getComponentRecords: vi.fn(() => of([])),
      getReadiness: vi.fn(() => of(readiness)),
      getRecordHistory: vi.fn(() => of([
        { id: 1, entityType: 'ComponentResult', entityId: 9, action: 'Created', toStatus: 'Draft', changedBy: 'eng50', changedAt: '2026-11-01T10:00:00Z' },
        { id: 2, entityType: 'ComponentResult', entityId: 9, action: 'Submitted', fromStatus: 'Draft', toStatus: 'Submitted', changedBy: 'eng50', changedAt: '2026-11-01T10:05:00Z' }
      ])),
      saveComponentRecord: vi.fn(),
      submitComponentRecord: vi.fn(),
      verifyComponentRecord: vi.fn()
    };
  }

  async function create(role: string, canEdit: boolean) {
    const api = apiStub();
    await TestBed.configureTestingModule({
      imports: [PowerAcKpiComponent],
      providers: [
        { provide: PowerAcKpiService, useValue: api },
        { provide: PowerAndACService, useValue: {
          getByYear: vi.fn(() => of([
            { id: 41, designation: 'NW/WPC-1', year: 2026, month: 1, scheduled: 13, attended: 12, cumulative_Sched: 13, cumulative_Achieved: 12, isVerified: true }
          ])),
          toggleVerified: vi.fn()
        } },
        { provide: AuthService, useValue: { getRole: () => role, canEditPage: () => canEdit } }
      ]
    }).compileComponents();
    const fixture = TestBed.createComponent(PowerAcKpiComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, api, el: fixture.nativeElement as HTMLElement };
  }

  afterEach(() => TestBed.resetTestingModule());

  it('shows overall, component, allocation and points results with statuses', async () => {
    const { el } = await create('Admin', false);
    const text = el.textContent ?? '';
    expect(text).toContain('WPC-1');
    expect(text).toContain('92.50%');      // composite
    expect(text).toContain('3500.00');     // maximum points
    expect(text).toContain('3237.50');     // points achieved
    expect(text).toContain('Pending');     // WPC-2 status and pending safety
    expect(text).toContain('Power & A/C KPI Overview');
    // Same structure as the Tower / Routine Maintenance platform pages.
    expect(el.querySelector('.kpi-page .hero-panel h1')).not.toBeNull();
    expect(el.querySelector('.data-card header .header-filters')).not.toBeNull();
    const overview = el.querySelector('table.overview-table:not(.readiness-table)') as HTMLTableElement;
    expect(Array.from(overview.querySelectorAll('thead th')).map(th => th.textContent?.trim()))
      .toEqual(['KPI', 'Target', 'Weight', 'WPC-1', 'WPC-2']);
    expect(overview.querySelectorAll('tbody tr').length).toBe(9); // 4 components + composite, allocation, max, points, status
    // Component detail cards: Month rows and a Distribution/Achievement column pair per engineer.
    const titles = Array.from(el.querySelectorAll('.data-card h2')).map(h => h.textContent?.trim());
    expect(titles).toEqual(expect.arrayContaining(['Preventive Maintenance', 'Critical Safety', 'Corrective Action and Maintenance Record', 'Power System Availability']));
    expect(text).toContain('Scheduled');
    expect(text).toContain('Attended');
    expect(el.querySelector('.global-issues')?.textContent).toContain('Area allocation is missing');
  });

  it('shows Calculate & save only to users allowed to calculate', async () => {
    const viewer = await create('User', false);
    expect(viewer.el.querySelector('#pac-calculate')).toBeNull();
    TestBed.resetTestingModule();
    const admin = await create('Admin', false);
    expect(admin.el.querySelector('#pac-calculate')).not.toBeNull();
  });

  it('validates allocation values and only saves editable changed rows', async () => {
    const { component, api } = await create('PlatformAdmin', true);
    component.setTab('allocations');
    const row = component.allocationRows[0];

    row.input = -1;
    component.validateAllocation(row);
    expect(row.error).not.toBe('');
    component.saveAllocations();
    expect(api.saveAllocations).not.toHaveBeenCalled();

    row.input = 12.5;
    component.validateAllocation(row);
    expect(row.error).toBe('');
    component.saveAllocations();
    expect(api.saveAllocations).toHaveBeenCalledWith(component.year, component.month, [
      { engineerGroupId: 1, allocationValue: 12.5, remarks: null }
    ]);
  });

  it('requires an incident count for Critical Safety and paired counts for other components', async () => {
    const { component } = await create('PlatformAdmin', true);
    const row = {
      group: groups[0], record: null, applicableCount: null, completedCount: null,
      incidentCount: null, achievementPercent: null, remarks: '', error: '', saving: false
    };

    component.dataComponentId = 2; // SAFETY
    expect(component.validateRow(row)).toContain('incident count');
    row.incidentCount = 0 as never;
    expect(component.validateRow(row)).toBe('');

    component.dataComponentId = 3; // CORRECTIVE
    row.applicableCount = 10 as never;
    expect(component.validateRow(row)).toContain('together');
    row.completedCount = 8 as never;
    expect(component.validateRow(row)).toBe('');
  });

  it('shows readiness per group and never labels an unapproved group as score-ready', async () => {
    const { el, component } = await create('User', false);
    const table = el.querySelector('table.readiness-table') as HTMLTableElement;
    expect(table).not.toBeNull();
    const text = table.textContent ?? '';
    expect(text).toContain('Awaiting approval');
    expect(text).toContain('Not configured');
    expect(text).not.toContain('Score-ready');
    expect(el.querySelector('.decisions')?.textContent).toContain('Critical Safety');
    // Allocation check: entered for WPC-1, missing for WPC-2.
    const allocationRow = component.readinessRows.find(r => r.key === 'ALLOCATION')!;
    expect(component.readinessCheck(allocationRow, 1)).toBe(true);
    expect(component.readinessCheck(allocationRow, 2)).toBe(false);
    const approvedRow = component.readinessRows.find(r => r.key === 'APPROVED')!;
    expect(component.readinessCheck(approvedRow, 1)).toBe(false);
  });

  it('loads the audit history of a record on demand', async () => {
    const { component, api } = await create('PlatformAdmin', true);
    const row = {
      group: groups[0], record: { id: 9 } as any, applicableCount: null, completedCount: null,
      incidentCount: 0, achievementPercent: null, remarks: '', error: '', saving: false
    };
    component.toggleHistory(row);
    expect(api.getRecordHistory).toHaveBeenCalledWith(9);
    expect(component.history[9]?.map(h => h.action)).toEqual(['Created', 'Submitted']);
    component.toggleHistory(row);
    expect(component.history[9]).toBeUndefined();
  });

  it('lists only the engineer groups the user may manage for data entry', async () => {
    const { component } = await create('PlatformAdmin', true);
    expect(component.manageableGroups.map(g => g.code)).toEqual(['WPC-1']);
    expect(component.entryComponents.map(c => c.code)).toEqual(['SAFETY', 'CORRECTIVE', 'AVAILABILITY']);
  });
});

describe('Power & AC helpers', () => {
  it('sums weights without floating point noise', () => {
    expect(sumWeights([{ weightPercent: 33.3333 }, { weightPercent: 33.3333 }, { weightPercent: 33.3334 }])).toBe(100);
    expect(sumWeights([{ weightPercent: 25 }, { weightPercent: null }])).toBe(25);
  });

  it('accepts allocations from 0 with at most 4 decimals', () => {
    expect(isValidAllocation(0)).toBe(true);
    expect(isValidAllocation(12.3456)).toBe(true);
    expect(isValidAllocation(1.0005)).toBe(true);
    expect(isValidAllocation(1.00005)).toBe(false);
    expect(isValidAllocation(-1)).toBe(false);
    expect(isValidAllocation('')).toBe(false);
    expect(isValidAllocation(null)).toBe(false);
  });
});
