/* File: enterprise-kpi.service.ts
   Description: Enterprise KPI admin service
   Purpose: CRUD operations for Enterprise KPI definitions.
*/

import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface EnterpriseKpiRecord {
  id: number;
  networkEngineerKpi: string;
  division?: string;
  section?: string;
  kpiPercent?: number;
}

export interface CreateEnterpriseKpi {
  networkEngineerKpi: string;
  division?: string;
  section?: string;
  kpiPercent?: number;
}

export interface EnterpriseTargetDto {
  id: number;
  enterpriseKpiId: number;
  section: string;
  month: number;
  year: number;
}

export interface CreateEnterpriseTargetDto {
  enterpriseKpiId: number;
  section: string;
  month: number;
  year: number;
}

@Injectable({ providedIn: 'root' })
export class EnterpriseKpiService {
  private readonly apiUrl = environment.apiUrl;

  // Injects the required dependencies.
  constructor(private http: HttpClient) {}

  // Retrieves all Enterprise KPI definitions
  getAll(): Observable<EnterpriseKpiRecord[]> {
    return this.http.get<EnterpriseKpiRecord[]>(`${this.apiUrl}/EnterpriseKpi`);
  }

  // Creates a new Enterprise KPI definition
  create(payload: CreateEnterpriseKpi): Observable<EnterpriseKpiRecord> {
    return this.http.post<EnterpriseKpiRecord>(`${this.apiUrl}/EnterpriseKpi`, payload);
  }

  // Updates an existing Enterprise KPI definition by ID
  update(id: number, payload: CreateEnterpriseKpi): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/EnterpriseKpi/${id}`, payload);
  }

  // Deletes an Enterprise KPI definition by ID
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/EnterpriseKpi/${id}`);
  }

  // Target Methods
  // Retrieves all target configurations for Enterprise KPIs
  getTargets(): Observable<EnterpriseTargetDto[]> {
    return this.http.get<EnterpriseTargetDto[]>(`${this.apiUrl}/enterprise-targets`);
  }

  // Creates a new Enterprise KPI target configuration
  createTarget(payload: CreateEnterpriseTargetDto): Observable<EnterpriseTargetDto> {
    return this.http.post<EnterpriseTargetDto>(`${this.apiUrl}/enterprise-targets`, payload);
  }

  // Updates an existing target configuration by ID
  updateTarget(id: number, payload: CreateEnterpriseTargetDto): Observable<void> {
    return this.http.put<void>(`${this.apiUrl}/enterprise-targets/${id}`, payload);
  }
}
