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
  areaAllocation: number | null;
  isVerified: boolean;
  enteredBy: string | null;
  verifiedBy: string | null;
}

export interface PowerAcSaveItem {
  designation: string;
  achievements: Record<string, number | null>;
  areaAllocation: number | null;
}

export interface PowerAcGroupResult {
  code: string;
  label: string;
  designation: string;
  achievement: Record<string, number | null>;
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

  verify(year: number, month: number): Observable<{ verified: number; skipped: number; message: string }> {
    return this.http.post<{ verified: number; skipped: number; message: string }>(`${this.base}/data/verify`, {}, { params: { year, month } });
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

  getResults(year: number, month: number): Observable<PowerAcResults> {
    return this.http.get<PowerAcResults>(`${this.base}/results`, { params: { year, month } });
  }
}
