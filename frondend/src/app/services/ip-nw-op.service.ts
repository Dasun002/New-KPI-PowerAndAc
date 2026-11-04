/* File: ip-nw-op.service.ts
   Description: IP Network Operations KPI service
   Purpose: Manages IP NW OP KPI data with area-based metrics
   and detailed node availability tracking.
*/

import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

/* ========== DATA INTERFACES ========== */

/* IP NW OP KPI data structure */
export interface IpNwOpKpiDto {
  /* Unique KPI identifier */
  id: number;
  /* Network engineer responsible */
  network_engineer_kpi: string;
  /* Division/department */
  division: string;
  /* Section/team */
  section: string;
  /* Overall KPI percentage */
  kpi_percent: number;
  /* Last update timestamp */
  updated_at?: string;
  /* Minutes unavailable by area code */
  unavailable_minutes?: Record<string, number | null>;
  /* Total minutes by area code */
  total_minutes?: Record<string, number | null>;
  /* Total nodes by area code */
  total_nodes?: Record<string, number | null>;
}

/* Metric payload for IP NW OP updates */
export interface IpNwOpMetricPayload {
  /* Minutes unavailable */
  unavailableMinutes?: number | null;
  /* Total operational minutes */
  totalMinutes?: number | null;
  /* Total number of nodes */
  totalNodes?: number | null;
}

/* Individual metric record */
export interface IpNwOpMetric {
  /* Foreign key to KPI */
  ip_nw_op_kpi_id: number;
  /* Area code identifier */
  area_code: string;
  /* Metric month */
  month: number;
  /* Metric year */
  year: number;
  /* Minutes unavailable */
  unavailable_minutes?: number | null;
  /* Total minutes */
  total_minutes?: number | null;
  /* Total nodes */
  total_nodes?: number | null;
}

/* ========== IP NW OP SERVICE ========== */

@Injectable({ providedIn: 'root' })
export class IpNwOpService {
  /* Backend API base URL */
  //private readonly apiBase = 'http://localhost:5043/ip-nw-op';
  private readonly apiBase = `${environment.apiUrl}/ip-nw-op`;
  // Injects the required dependencies.
  constructor(private http: HttpClient) {}

  // Retrieves all IP NW OP KPIs, with optional filters for month, year, and area
  getAll(month?: number, year?: number, area?: string): Observable<IpNwOpKpiDto[]> {
    let params = new HttpParams();
    if (month !== undefined) params = params.set('month', month.toString());
    if (year !== undefined) params = params.set('year', year.toString());
    if (area) params = params.set('area', area);
    
    return this.http.get<IpNwOpKpiDto[]>(`${this.apiBase}/`, { params });
  }

  // Creates a new IP NW OP KPI record
  add(data: Omit<IpNwOpKpiDto, 'id' | 'updated_at'>): Observable<IpNwOpKpiDto> {
    return this.http.post<IpNwOpKpiDto>(`${this.apiBase}/add`, data);
  }

  // Updates an existing IP NW OP KPI record partially
  update(id: number, data: Partial<Omit<IpNwOpKpiDto, 'id'>>): Observable<IpNwOpKpiDto> {
    return this.http.put<IpNwOpKpiDto>(`${this.apiBase}/update/${id}`, data);
  }

  // Deletes an IP NW OP KPI record by ID
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiBase}/delete/${id}`);
  }

  // Inserts or updates metric data for a specific IP NW OP KPI and area code
  upsertMetric(
    kpiId: number,
    areaCode: string,
    month: number,
    year: number,
    payload: IpNwOpMetricPayload
  ): Observable<IpNwOpKpiDto> {
    let params = new HttpParams();
    params = params.set('month', month.toString());
    params = params.set('year', year.toString());
    
    return this.http.put<IpNwOpKpiDto>(
      `${this.apiBase}/metrics/${kpiId}/${areaCode}`,
      payload,
      { params }
    );
  }

  // Retrieves IP NW OP metrics for a specified time period and optional area code
  getMetrics(month: number, year: number, areaCode?: string): Observable<IpNwOpMetric[]> {
    let params = new HttpParams();
    params = params.set('month', month.toString());
    params = params.set('year', year.toString());
    if (areaCode) params = params.set('areaCode', areaCode);
    
    return this.http.get<IpNwOpMetric[]>(`${this.apiBase}/metrics`, { params });
  }
}
