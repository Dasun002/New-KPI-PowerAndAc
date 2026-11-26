/* File: power-ac-kpi.service.ts
   API client for the Power & A/C KPI. All data is in one table (dbo.PowerAcPmData), read and written
   through api/power-ac: data rows, save, verify, and calculated results. */
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface PowerAcComponentInfo {
  code: string;
  name: string;
  weightPercent: number;
  target: string;
  targetPercent: number;
}

// A configured Power & A/C KPI (a KpiDefinition row with Category PowerAndAC).
export interface PowerAcKpiRow {
  id: number;
  name: string;
  code: string;
  category: string;
  target: string;
  unit: string;
  weightage: number;
  included: boolean;
}

export interface PowerAcKpiInput {
  name: string;
  code: string;
  target: string;
  unit: string;
  weightage: number;
  included?: boolean;
}

// One engineer group's stored values for a month. Achievements holds one value per configured KPI code.
export interface PowerAcDataRow {
  designation: string;
  engineerGroupCode: string;
  label: string;
  achievements: Record<string, number | null>;
  // Typed inputs for the rule-scored KPIs: SAFETY = incident count, AVAILABILITY = actual %.
  inputs: Record<string, number | null>;
  // Rule-scored KPIs held as an old percentage only (no typed input yet).
  legacyCodes: string[];
  // Verified | VerifiedNeedsReentry | ReadyToVerify | Incomplete, and the requirements still missing.
  verificationStatus: string;
  verificationMissing: string[];
  areaAllocation: number | null;
  isVerified: boolean;
  enteredBy: string | null;
  verifiedBy: string | null;
}

export interface PowerAcSaveItem {
  designation: string;
  // Achievement % for the other KPIs. Do not include SAFETY or AVAILABILITY here.
  achievements: Record<string, number | null>;
  // Only the rule-scored KPIs that were edited. A key that is missing is left unchanged on the server.
  inputs?: Record<string, number | null>;
  areaAllocation: number | null;
}

// Monthly distribution (scheduled) and achievement (attended) counts for one engineer group and month.
export interface PowerAcMonthlyCell {
  month: number;
  designation: string;
  scheduled: number | null;
  attended: number | null;
  hasRow: boolean;
  isVerified: boolean;
}

export interface PowerAcGroupResult {
  code: string;
  label: string;
  designation: string;
  achievement: Record<string, number | null>;
  legacyCodes: string[];
  allocationValue: number | null;
  isVerified: boolean;
  composite: number | null;
  maximumPoints: number | null;
  pointsAchieved: number | null;
  status: 'Complete' | 'Incomplete';
  issues: string[];
}

export interface PowerAcResults {
  year: number;
  month: number;
  pointsApplicable: number | null;
  totalAllocation: number | null;
  completeCount: number;
  incompleteCount: number;
  totalMaximumPoints: number | null;
  totalPointsAchieved: number | null;
  components: PowerAcComponentInfo[];
  groups: PowerAcGroupResult[];
}

// Result of "Verify values". A group with a missing value is not verified and is listed in issues.
export interface PowerAcVerifyResult {
  message: string;
  verified: number;
  skipped: number;
  verifiedDesignations: string[];
  issues: { designation: string; missing: string[]; message: string }[];
}

export interface PowerAcOverallStatus {
  year: number;
  months: {
    month: number;
    lastValuesChanged: string | null;
    lastOverallCalculated: string | null;
    needsRecalculation: boolean;
  }[];
}

@Injectable({ providedIn: 'root' })
export class PowerAcKpiService {
  private readonly base = `${environment.apiUrl}/power-ac`;

  constructor(private readonly http: HttpClient) {}

  getData(year: number, month: number): Observable<PowerAcDataRow[]> {
    return this.http.get<PowerAcDataRow[]>(`${this.base}/data`, { params: { year, month } });
  }

  saveData(year: number, month: number, items: PowerAcSaveItem[]): Observable<{ saved: number; message: string }> {
    return this.http.put<{ saved: number; message: string }>(`${this.base}/data`, { year, month, items });
  }

  verify(year: number, month: number): Observable<PowerAcVerifyResult> {
    return this.http.post<PowerAcVerifyResult>(`${this.base}/data/verify`, {}, { params: { year, month } });
  }

  getMonthly(year: number): Observable<PowerAcMonthlyCell[]> {
    return this.http.get<PowerAcMonthlyCell[]>(`${this.base}/data/monthly`, { params: { year } });
  }

  toggleVerified(year: number, month: number, designation: string): Observable<{ designation: string; isVerified: boolean }> {
    return this.http.patch<{ designation: string; isVerified: boolean }>(`${this.base}/data/toggle-verified`, {}, { params: { year, month, designation } });
  }

  // ----- admin configuration of the KPIs -----
  listKpis(): Observable<PowerAcKpiRow[]> {
    return this.http.get<PowerAcKpiRow[]>(`${this.base}/config/kpis`);
  }

  addKpi(input: PowerAcKpiInput): Observable<PowerAcKpiRow> {
    return this.http.post<PowerAcKpiRow>(`${this.base}/config/kpis`, input);
  }

  updateKpi(id: number, input: PowerAcKpiInput): Observable<PowerAcKpiRow> {
    return this.http.put<PowerAcKpiRow>(`${this.base}/config/kpis/${id}`, input);
  }

  deleteKpi(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/config/kpis/${id}`);
  }

  // Per month: when the Power & A/C values last changed, when the overall result was last calculated, and whether it needs a recalculation.
  getOverallStatus(year: number): Observable<PowerAcOverallStatus> {
    return this.http.get<PowerAcOverallStatus>(`${this.base}/overall-status`, { params: { year } });
  }

  getResults(year: number, month: number): Observable<PowerAcResults> {
    return this.http.get<PowerAcResults>(`${this.base}/results`, { params: { year, month } });
  }
}
