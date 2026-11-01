/* File: power-ac-kpi.service.ts
   Description: API client for the four-component Power & AC KPI (api/power-ac/...).
   Preventive Maintenance source rows still use PowerAndACService (api/powerandac).
*/
import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export type PowerAcPeriodType = 'Month' | 'Quarter';
export type PowerAcStatus = 'Complete' | 'Incomplete' | 'Pending';
export type PowerAcRecordStatus = 'Draft' | 'Submitted' | 'Verified' | 'Rejected';

export interface PowerAcComponent {
  id: number;
  code: string;
  name: string;
  targetValue: number;
  targetUnit: string;
  dataSource: 'PowerAndAC' | 'ComponentResult';
  scoringMethod: string;
  scoringParameter: number | null;
  scoringApproved: boolean;
  approvedBy?: string | null;
  approvedAt?: string | null;
  sortOrder: number;
  isActive: boolean;
  currentWeightPercent: number | null;
  allowedScoringMethods: string[];
  versionId?: number | null;
  versionEffectiveYear?: number | null;
  versionEffectiveMonth?: number | null;
  hasEffectiveVersion?: boolean;
}

export interface PowerAcComponentVersion {
  id: number;
  componentId: number;
  componentCode: string;
  effectiveYear: number;
  effectiveMonth: number;
  targetValue: number;
  scoringMethod: string;
  scoringParameter: number | null;
  scoringApproved: boolean;
  approvedBy?: string | null;
  approvedAt?: string | null;
  approvalNote?: string | null;
  createdAt: string;
  createdBy?: string | null;
  updatedAt?: string | null;
  updatedBy?: string | null;
}

export interface PowerAcAuditEntry {
  id: number;
  entityType: string;
  entityId: number;
  action: string;
  fromStatus?: string | null;
  toStatus?: string | null;
  detailsJson?: string | null;
  comment?: string | null;
  changedBy?: string | null;
  changedAt: string;
}

export type PowerAcReadinessState = 'NotConfigured' | 'DataIncomplete' | 'AwaitingVerification' | 'AwaitingApproval' | 'ScoreReady';

export interface PowerAcComponentReadiness {
  componentId: number;
  code: string;
  hasData: boolean;
  dataComplete: boolean;
  verified: boolean;
  approved: boolean;
  scoringMethod: string;
  versionId?: number | null;
  missing: string[];
}

export interface PowerAcGroupReadiness {
  engineerGroupId: number;
  code: string;
  mappingVerified: boolean;
  allocationEntered: boolean;
  state: PowerAcReadinessState;
  resultStatus: string;
  components: PowerAcComponentReadiness[];
  blockers: string[];
}

export interface PowerAcReadinessReport {
  settingsConfigured: boolean;
  pointsMethodApproved: boolean;
  requireVerifiedData: boolean;
  weightsValid: boolean;
  weightTotal: number;
  allocationsEntered: number;
  groupCount: number;
  allocationsComplete: boolean;
  approvedComponentCount: number;
  componentCount: number;
  pendingBusinessDecisions: string[];
  groups: PowerAcGroupReadiness[];
  stateCounts: Record<PowerAcReadinessState, number>;
}

export interface PowerAcWeight {
  id: number;
  componentId: number;
  componentCode: string;
  effectiveYear: number;
  effectiveMonth: number;
  weightPercent: number;
  createdAt: string;
  createdBy?: string | null;
}

export interface PowerAcEffectiveWeights {
  year: number;
  month: number;
  total: number;
  isValid: boolean;
  effective: PowerAcWeight[];
  history: PowerAcWeight[];
}

export interface PowerAcSetting {
  id: number;
  effectiveYear: number;
  effectiveMonth: number;
  kpiDefinitionId: number;
  kpiName?: string | null;
  pointsApplicable?: number | null;
  pointsMethod: string;
  compositeTarget: number;
  pointsMethodApproved: boolean;
  requireVerifiedData: boolean;
  createdAt: string;
  createdBy?: string | null;
}

export interface SavePowerAcSetting {
  effectiveYear: number;
  effectiveMonth: number;
  kpiDefinitionId: number;
  pointsMethod: string;
  compositeTarget: number;
  pointsMethodApproved: boolean;
  requireVerifiedData: boolean;
}

export interface PowerAcEngineerGroup {
  id: number;
  code: string;
  workbookLabel: string;
  workbookAllocationCell: string;
  regionDataId: number | null;
  networkEngineer?: string | null;
  leaCode?: string | null;
  region?: string | null;
  province?: string | null;
  engineerName?: string | null;
  pmDesignation?: string | null;
  mappingVerified: boolean;
  mappingNote?: string | null;
  sortOrder: number;
  isActive: boolean;
  canManage: boolean;
}

export interface PowerAcGroupAssignment {
  id: number;
  engineerGroupId: number;
  engineerGroupCode: string;
  userId: number;
  userName?: string | null;
  serviceId?: string | null;
  assignedAt: string;
  assignedBy?: string | null;
}

export interface PowerAcAllocation {
  engineerGroupId: number;
  engineerGroupCode: string;
  workbookLabel: string;
  workbookAllocationCell: string;
  networkEngineer?: string | null;
  leaCode?: string | null;
  mappingVerified: boolean;
  allocationId?: number | null;
  allocationValue: number | null;
  effectiveYear?: number | null;
  effectiveMonth?: number | null;
  remarks?: string | null;
  updatedAt?: string | null;
  updatedBy?: string | null;
  canEdit: boolean;
}

export interface PowerAcAllocationSummary {
  year: number;
  month: number;
  total: number;
  missingCount: number;
  isComplete: boolean;
  items: PowerAcAllocation[];
}

export interface PowerAcComponentRecord {
  id: number;
  engineerGroupId: number;
  engineerGroupCode: string;
  year: number;
  month: number;
  componentId: number;
  componentCode: string;
  applicableCount: number | null;
  completedCount: number | null;
  incidentCount: number | null;
  achievementPercent: number | null;
  status: PowerAcRecordStatus;
  remarks?: string | null;
  reviewComment?: string | null;
  submittedBy?: string | null;
  submittedAt?: string | null;
  verifiedBy?: string | null;
  verifiedAt?: string | null;
  createdAt: string;
  createdBy?: string | null;
  updatedAt?: string | null;
  updatedBy?: string | null;
  canEdit: boolean;
}

export interface SavePowerAcComponentRecord {
  engineerGroupId: number;
  year: number;
  month: number;
  componentId: number;
  applicableCount: number | null;
  completedCount: number | null;
  incidentCount: number | null;
  achievementPercent: number | null;
  remarks: string | null;
}

export interface PowerAcComponentScore {
  componentId: number;
  code: string;
  name: string;
  weightPercent: number | null;
  scoringMethod: string;
  applicableCount: number | null;
  completedCount: number | null;
  incidentCount: number | null;
  achievementRatio: number | null;
  score: number | null;
  weightedScore: number | null;
  status: PowerAcStatus;
  message?: string | null;
}

export interface PowerAcGroupResult {
  engineerGroupId: number;
  engineerGroupCode: string;
  workbookLabel: string;
  networkEngineer?: string | null;
  leaCode?: string | null;
  status: PowerAcStatus;
  compositeAchievement: number | null;
  pointsApplicable: number | null;
  allocationValue: number | null;
  totalAllocation: number | null;
  maximumPoints: number | null;
  pointsAchieved: number | null;
  issues: string[];
  warnings: string[];
  components: PowerAcComponentScore[];
}

export interface PowerAcKpiResults {
  year: number;
  periodType: PowerAcPeriodType;
  periodNumber: number;
  isPreview: boolean;
  calculatedAt?: string | null;
  calculatedBy?: string | null;
  pointsMethod: string;
  compositeTarget: number;
  pointsMethodApproved: boolean;
  requireVerifiedData: boolean;
  pointsApplicable: number | null;
  totalAllocation: number | null;
  allocationsComplete: boolean;
  weightsValid: boolean;
  weightTotal: number;
  globalIssues: string[];
  components: PowerAcComponent[];
  groups: PowerAcGroupResult[];
  completeCount: number;
  incompleteCount: number;
  pendingCount: number;
  totalPointsAchieved: number | null;
  totalMaximumPoints: number | null;
}

@Injectable({ providedIn: 'root' })
export class PowerAcKpiService {
  private readonly base = `${environment.apiUrl}/power-ac`;

  constructor(private readonly http: HttpClient) {}

  // ----- configuration -----
  getComponents(year?: number, month?: number): Observable<PowerAcComponent[]> {
    return this.http.get<PowerAcComponent[]>(`${this.base}/config/components`, { params: this.periodParams(year, month) });
  }

  // Creates or updates the scoring version effective from effectiveYear/effectiveMonth.
  updateComponent(id: number, body: {
    effectiveYear: number; effectiveMonth: number; approvalNote?: string | null;
    targetValue: number; scoringMethod: string; scoringParameter: number | null;
    scoringApproved: boolean; isActive: boolean; sortOrder: number;
  }): Observable<PowerAcComponent> {
    return this.http.put<PowerAcComponent>(`${this.base}/config/components/${id}`, body);
  }

  getComponentVersions(id: number): Observable<PowerAcComponentVersion[]> {
    return this.http.get<PowerAcComponentVersion[]>(`${this.base}/config/components/${id}/versions`);
  }

  getAudit(entityType?: string, entityId?: number, take = 200): Observable<PowerAcAuditEntry[]> {
    let params = new HttpParams().set('take', take);
    if (entityType) params = params.set('entityType', entityType);
    if (entityId) params = params.set('entityId', entityId);
    return this.http.get<PowerAcAuditEntry[]>(`${this.base}/config/audit`, { params });
  }

  getWeights(year: number, month: number): Observable<PowerAcEffectiveWeights> {
    return this.http.get<PowerAcEffectiveWeights>(`${this.base}/config/weights`, { params: this.periodParams(year, month) });
  }

  saveWeights(effectiveYear: number, effectiveMonth: number, weights: { componentId: number; weightPercent: number }[]): Observable<unknown> {
    return this.http.post(`${this.base}/config/weights`, { effectiveYear, effectiveMonth, weights });
  }

  getSettings(): Observable<PowerAcSetting[]> {
    return this.http.get<PowerAcSetting[]>(`${this.base}/config/settings`);
  }

  saveSetting(body: SavePowerAcSetting): Observable<unknown> {
    return this.http.post(`${this.base}/config/settings`, body);
  }

  getEngineerGroups(): Observable<PowerAcEngineerGroup[]> {
    return this.http.get<PowerAcEngineerGroup[]>(`${this.base}/config/engineer-groups`);
  }

  updateEngineerGroup(id: number, body: {
    regionDataId: number | null; pmDesignation: string | null; mappingVerified: boolean; mappingNote: string | null; isActive: boolean;
  }): Observable<PowerAcEngineerGroup> {
    return this.http.put<PowerAcEngineerGroup>(`${this.base}/config/engineer-groups/${id}`, body);
  }

  getAssignments(): Observable<PowerAcGroupAssignment[]> {
    return this.http.get<PowerAcGroupAssignment[]>(`${this.base}/config/assignments`);
  }

  createAssignment(engineerGroupId: number, userId: number): Observable<PowerAcGroupAssignment> {
    return this.http.post<PowerAcGroupAssignment>(`${this.base}/config/assignments`, { engineerGroupId, userId });
  }

  deleteAssignment(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/config/assignments/${id}`);
  }

  // ----- allocations -----
  getAllocations(year: number, month: number): Observable<PowerAcAllocationSummary> {
    return this.http.get<PowerAcAllocationSummary>(`${this.base}/allocations`, { params: this.periodParams(year, month) });
  }

  saveAllocations(effectiveYear: number, effectiveMonth: number,
    items: { engineerGroupId: number; allocationValue: number; remarks: string | null }[]): Observable<unknown> {
    return this.http.put(`${this.base}/allocations`, { effectiveYear, effectiveMonth, items });
  }

  // ----- component data -----
  getComponentRecords(filter: { year: number; month?: number; componentId?: number; engineerGroupId?: number; status?: string }): Observable<PowerAcComponentRecord[]> {
    let params = new HttpParams().set('year', filter.year);
    if (filter.month) params = params.set('month', filter.month);
    if (filter.componentId) params = params.set('componentId', filter.componentId);
    if (filter.engineerGroupId) params = params.set('engineerGroupId', filter.engineerGroupId);
    if (filter.status) params = params.set('status', filter.status);
    return this.http.get<PowerAcComponentRecord[]>(`${this.base}/component-results`, { params });
  }

  saveComponentRecord(body: SavePowerAcComponentRecord): Observable<PowerAcComponentRecord> {
    return this.http.put<PowerAcComponentRecord>(`${this.base}/component-results`, body);
  }

  submitComponentRecord(id: number): Observable<PowerAcComponentRecord> {
    return this.http.post<PowerAcComponentRecord>(`${this.base}/component-results/${id}/submit`, {});
  }

  verifyComponentRecord(id: number, approve: boolean, comment: string | null): Observable<PowerAcComponentRecord> {
    return this.http.post<PowerAcComponentRecord>(`${this.base}/component-results/${id}/verify`, { approve, comment });
  }

  getRecordHistory(id: number): Observable<PowerAcAuditEntry[]> {
    return this.http.get<PowerAcAuditEntry[]>(`${this.base}/component-results/${id}/history`);
  }

  getReadiness(year: number, periodType: PowerAcPeriodType, period: number): Observable<PowerAcReadinessReport> {
    return this.http.get<PowerAcReadinessReport>(`${this.base}/kpi/readiness`, { params: this.resultParams(year, periodType, period) });
  }

  // ----- results -----
  preview(year: number, periodType: PowerAcPeriodType, period: number): Observable<PowerAcKpiResults> {
    return this.http.get<PowerAcKpiResults>(`${this.base}/kpi/preview`, { params: this.resultParams(year, periodType, period) });
  }

  calculate(year: number, periodType: PowerAcPeriodType, period: number): Observable<PowerAcKpiResults> {
    return this.http.post<PowerAcKpiResults>(`${this.base}/kpi/calculate`, {}, { params: this.resultParams(year, periodType, period) });
  }

  getResults(year: number, periodType: PowerAcPeriodType, period: number): Observable<PowerAcKpiResults> {
    return this.http.get<PowerAcKpiResults>(`${this.base}/kpi/results`, { params: this.resultParams(year, periodType, period) });
  }

  private periodParams(year?: number, month?: number): HttpParams {
    let params = new HttpParams();
    if (year) params = params.set('year', year);
    if (month) params = params.set('month', month);
    return params;
  }

  private resultParams(year: number, periodType: PowerAcPeriodType, period: number): HttpParams {
    return new HttpParams().set('year', year).set('periodType', periodType).set('period', period);
  }
}

// Helpers shared by the Power & AC components and their tests.
export function sumWeights(weights: { weightPercent: number | null | undefined }[]): number {
  return Math.round(weights.reduce((sum, w) => sum + (Number(w.weightPercent) || 0), 0) * 10000) / 10000;
}

export function isValidAllocation(value: unknown): boolean {
  if (value === null || value === undefined || value === '') return false;
  const n = Number(value);
  if (!Number.isFinite(n) || n < 0 || n > 1000000) return false;
  // At most 4 decimal places (matches DECIMAL(18,4)); tolerance avoids binary floating-point noise.
  return Math.abs(n * 10000 - Math.round(n * 10000)) < 1e-6;
}
