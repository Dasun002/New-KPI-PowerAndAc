/* File: otn-op2.service.ts
   Description: OTN Operations 2 KPI service
   Purpose: Manages OTN OP2 KPI data with link failure tracking.
*/

import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

/* ========== DATA INTERFACES ========== */

/* OTN Operations KPI */
export interface OtnOpKpi {
  /* Unique KPI identifier */
  id: number;
  /* Network engineer responsible */
  networkEngineerKpi: string;
  /* Division/department */
  division?: string;
  /* Section/team */
  section?: string;
  /* Overall KPI percentage */
  kpiPercent?: number;
}

/* OTN Operations KPI creation payload */
export interface CreateOtnOpKpi {
  /* Network engineer responsible */
  networkEngineerKpi: string;
  /* Division/department */
  division?: string;
  /* Section/team */
  section?: string;
  /* Overall KPI percentage */
  kpiPercent?: number;
}

/* OTN OP2 metric - link failure tracking */
export interface OtnOp2Metric {
  /* Unique metric identifier */
  id: number;
  /* Foreign key to OTN OP2 KPI */
  otnOp2Id: number;
  /* Site name/code */
  site: string;
  /* Total failed links during period */
  totalFailedLinks: number;
  /* Links that did not violate SLA */
  linksSlaNotViolated: number;
  /* Metric year */
  year: number;
  /* Metric month */
  month: number;
}

/* ========== OTN OP2 SERVICE ========== */

@Injectable({
  providedIn: 'root'
})
export class OtnOp2Service {
  /* Backend API base URL */
  private readonly apiUrl = environment.apiUrl;

  // Injects the required dependencies.
  constructor(private http: HttpClient) {}

  // Retrieves all OtnOp2 KPIs
  getAllKpis(): Observable<OtnOpKpi[]> {
    return this.http.get<OtnOpKpi[]>(`${this.apiUrl}/OtnOp2`);
  }

  // Creates a new OtnOp2 KPI
  createKpi(payload: CreateOtnOpKpi): Observable<OtnOpKpi> {
    return this.http.post<OtnOpKpi>(`${this.apiUrl}/OtnOp2`, payload);
  }

  // Updates an existing OtnOp2 KPI by ID
  updateKpi(id: number, payload: CreateOtnOpKpi): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/OtnOp2/${id}`, payload);
  }

  // Deletes an OtnOp2 KPI by ID
  deleteKpi(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/OtnOp2/${id}`);
  }

  // Retrieves metrics for a specific OtnOp2 KPI by year and month
  getMetrics(id: number, year: number, month: number): Observable<OtnOp2Metric[]> {
    return this.http.get<OtnOp2Metric[]>(
      `${this.apiUrl}/OtnOp2/${id}/metrics?year=${year}&month=${month}`
    );
  }

  // Inserts or updates metrics for an OtnOp2 KPI
  upsertMetrics(id: number, metrics: OtnOp2Metric[]): Observable<any> {
    return this.http.post<any>(`${this.apiUrl}/OtnOp2/${id}/metrics`, metrics);
  }
}
