/* File: bb-anw.service.ts
   Description: Broadband Access Network KPI service
   Purpose: Manages BB ANW KPI data for platform and admin pages.
   Features: CRUD operations for BB ANW KPIs with node-level metrics
*/

import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

/* ========== DATA INTERFACES ========== */

/* Single node metric for BB ANW */
export interface BbAnwNodeDto {
  /* Node code identifier */
  nodeCode: string;
  /* Minutes node was unavailable */
  unavailableMinutes?: number | null;
  /* Total operational minutes */
  totalMinutes?: number | null;
  /* Total number of nodes */
  totalNodes?: number | null;
  /* Metric month */
  month: number;
  /* Metric year */
  year: number;
}

/* Full BB ANW KPI with detailed node metrics */
export interface BbAnwDto {
  /* Unique KPI identifier */
  id?: number;
  /* Network engineer responsible for KPI */
  networkEngineerKpi: string;
  /* Division/department */
  division?: string | null;
  /* Section/team */
  section?: string | null;
  /* Overall KPI percentage achievement */
  kpiPercent?: number | null;
  /* Node-level metrics */
  nodes?: BbAnwNodeDto[] | null;
}

/* Admin view - simplified BB ANW header data */
export interface BbAnwHeaderDto {
  /* Unique KPI identifier */
  id?: number;
  /* Network engineer responsible for KPI */
  networkEngineerKpi: string;
  /* Division/department */
  division?: string | null;
  /* Section/team */
  section?: string | null;
  /* Overall KPI percentage achievement */
  kpiPercent?: number | null;
}

/* ========== BB ANW SERVICE ========== */

@Injectable({ providedIn: 'root' })
export class BbAnwService {
  /* Backend API endpoint */
  private readonly apiUrl = `${environment.apiUrl}/bb-anw`;

  // Injects the required dependencies.
  constructor(private http: HttpClient) {}

  // ---------------------------
  // PLATFORM KPI (FULL)
  // ---------------------------
  // Retrieves all platform BB ANW KPIs, optionally scoped to a specific month and year
  getAll(month?: number, year?: number): Observable<BbAnwDto[]> {
    let params = new HttpParams();
    if (month) params = params.set('month', String(month));
    if (year) params = params.set('year', String(year));

    return this.http.get<BbAnwDto[]>(this.apiUrl, { params });
  }

  // Retrieves a specific BB ANW KPI by its ID
  getById(id: number): Observable<BbAnwDto> {
    return this.http.get<BbAnwDto>(`${this.apiUrl}/${id}`);
  }

  // Creates a new BB ANW KPI entry
  add(data: BbAnwDto): Observable<any> {
    return this.http.post(`${this.apiUrl}/add`, data);
  }

  // Updates an existing BB ANW KPI with the specified ID
  update(id: number, data: BbAnwDto): Observable<any> {
    return this.http.put(`${this.apiUrl}/update/${id}`, data);
  }

  // Deletes a BB ANW KPI entry by its ID
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/delete/${id}`);
  }

  // ---------------------------
  // ADMIN PAGE (HEADER ONLY)
  // ---------------------------
  // Retrieves a simplified header list of BB ANW KPIs for admin view
  getHeaders(): Observable<BbAnwHeaderDto[]> {
    return this.http.get<BbAnwHeaderDto[]>(`${this.apiUrl}/headers`);
  }

  // Adds a new header entry for BB ANW KPIs
  addHeader(data: BbAnwHeaderDto): Observable<any> {
    return this.http.post(`${this.apiUrl}/add-header`, data);
  }

  // Updates an existing BB ANW KPI header by ID
  updateHeader(id: number, data: BbAnwHeaderDto): Observable<any> {
    return this.http.put(`${this.apiUrl}/update-header/${id}`, data);
  }
}
