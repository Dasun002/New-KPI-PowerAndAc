/* File: other-kpi.service.ts
   Description: Other KPI admin service
   Purpose: CRUD operations for Other KPI definitions.
*/

import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface OtherKpiRecord {
  id: number;
  networkEngineerKpi: string;
  division?: string;
  section?: string;
  kpiPercent?: number;
}

export interface CreateOtherKpi {
  networkEngineerKpi: string;
  division?: string;
  section?: string;
  kpiPercent?: number;
}

@Injectable({ providedIn: 'root' })
export class OtherKpiService {
  private readonly apiUrl = environment.apiUrl;

  // Injects the required dependencies.
  constructor(private http: HttpClient) {}

  // Retrieves a list of all Other KPI records
  getAll(): Observable<OtherKpiRecord[]> {
    return this.http.get<OtherKpiRecord[]>(`${this.apiUrl}/OtherKpi`);
  }

  // Creates a new Other KPI record
  create(payload: CreateOtherKpi): Observable<OtherKpiRecord> {
    return this.http.post<OtherKpiRecord>(`${this.apiUrl}/OtherKpi`, payload);
  }

  // Updates an existing Other KPI record based on the ID
  update(id: number, payload: CreateOtherKpi): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/OtherKpi/${id}`, payload);
  }

  // Deletes an Other KPI record by its ID
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/OtherKpi/${id}`);
  }
}
