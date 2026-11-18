/* File: service-fulfilment-kpi.service.ts
   Description: Service Fulfilment KPI service
   Purpose: Manages service-level KPI definitions and metrics
   with area-based performance tracking.
*/

import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../environments/environment';

/* ========== DATA INTERFACES ========== */

/* Service Fulfilment KPI definition */
export interface ServiceFulfilmentKpiDto {
  /* Unique KPI identifier */
  id?: number | string;
  /* KPI name/description */
  kpi: string;
  /* Target value for KPI */
  target: string;
  /* Calculation method */
  calculation: string;
  /* Platform this KPI applies to */
  platform: string;
  /* Responsible DGM (Director General Manager) */
  responsibleDgm: string;
  /* Defined OLA (Operating Level Agreement) details */
  defineDoladetails?: string;
  /* Alternative field name for OLA details */
  definedoladetails?: string;
  /* Weight of this KPI in overall score */
  weightage: number;
  /* Data sources for this KPI */
  dataSources: string;
  /* Metric month */
  month: number;
  /* Metric year */
  year: number;
  /* Last update timestamp */
  updatedAt?: string;
  /* Display order in UI */
  displayOrder?: number;
}

/* API response for metric query */
interface ServiceFulfilmentMetricResponse {
  /* Unique metric identifier */
  id?: number | string;
  /* KPI name */
  kpi: string;
  /* Target value */
  target: string;
  /* Platform */
  platform: string;
  /* Responsible DGM */
  responsibleDgm: string;
  /* OLA details */
  definedoladetails?: string;
  /* KPI weightage */
  weightage: number;
  /* Area code for this metric */
  area: string;
  /* Achieved KPI value (0-100) */
  kpi_value: number;
  /* Metric month */
  month: number;
  /* Metric year */
  year: number;
}

/* Payload for creating/updating service fulfilment metrics */
export interface UpsertServiceFulfilmentMetricRequest {
  /* Service Fulfilment KPI definition ID */
  serviceFulfilmentKpiId: number;
  /* Area code where metric applies */
  areaCode: string;
  /* Achieved KPI value */
  kpiValue: number | null;
  /* Metric month */
  month: number;
  /* Metric year */
  year: number;
}

/* Service Fulfilment metric data structure */
export interface ServiceFulfilmentMetricDto {
  /* Unique metric identifier */
  id?: number | string;
  kpi: string;
  target: string;
  platform: string;
  responsibleDgm: string;
  definedoladetails?: string;
  weightage: number;
  area: string;
  kpiValue: number;
  month: number;
  year: number;
}

@Injectable({
  providedIn: 'root'
})
export class ServiceFulfilmentKpiService {
  private readonly apiUrl = `${environment.apiUrl}/service-fulfilment-kpi`;

  // Injects the required dependencies.
  constructor(private http: HttpClient) {}

  // Retrieves all Service Fulfilment KPIs, optionally filtered by month and year, and calculates display order
  getAll(month?: number, year?: number): Observable<ServiceFulfilmentKpiDto[]> {
    let params = new HttpParams();

    if (month) {
      params = params.set('month', month.toString());
    }

    if (year) {
      params = params.set('year', year.toString());
    }

    const options = params.keys().length ? { params } : {};
    return this.http
      .get<ServiceFulfilmentKpiDto[]>(this.apiUrl, options)
      .pipe(
        map((items) =>
          items.map((item, index) => {
            const definedOla =
              item.defineDoladetails ?? (item as any).definedoladetails ?? '';
            return {
              ...item,
              defineDoladetails: definedOla,
              definedoladetails: definedOla,
              displayOrder: (item as any).no ?? index + 1
            } as ServiceFulfilmentKpiDto;
          })
        )
      );
  }

  // Retrieves a specific Service Fulfilment KPI by ID
  getById(id: number | string): Observable<ServiceFulfilmentKpiDto> {
    return this.http.get<ServiceFulfilmentKpiDto>(`${this.apiUrl}/${id}`);
  }

  // Creates a new Service Fulfilment KPI entry
  add(data: ServiceFulfilmentKpiDto): Observable<ServiceFulfilmentKpiDto> {
    return this.http.post<ServiceFulfilmentKpiDto>(`${this.apiUrl}/add`, data);
  }

  // Updates an existing Service Fulfilment KPI by ID
  update(id: number | string, data: ServiceFulfilmentKpiDto): Observable<ServiceFulfilmentKpiDto> {
    return this.http.put<ServiceFulfilmentKpiDto>(`${this.apiUrl}/update/${id}`, data);
  }

  // Deletes a Service Fulfilment KPI by ID
  delete(id: number | string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/delete/${id}`);
  }

  // Retrieves Service Fulfilment metrics for a given period, optionally filtered by area
  getMetrics(month: number, year: number, area?: string): Observable<ServiceFulfilmentMetricDto[]> {
    let params = new HttpParams()
      .set('month', month.toString())
      .set('year', year.toString());

    if (area) {
      params = params.set('area', area);
    }

    return this.http
      .get<ServiceFulfilmentMetricResponse[]>(`${this.apiUrl}/metrics`, { params })
      .pipe(
        map((items) =>
          items.map((item) => ({
            id: item.id,
            kpi: item.kpi,
            target: item.target,
            platform: item.platform,
            responsibleDgm: item.responsibleDgm,
            definedoladetails: item.definedoladetails ?? (item as any).defineDoladetails,
            weightage: item.weightage,
            area: item.area,
            kpiValue: item.kpi_value,
            month: item.month,
            year: item.year
          }))
        )
      );
  }

  // Creates or updates a Service Fulfilment metric based on the provided payload
  upsertMetric(request: UpsertServiceFulfilmentMetricRequest): Observable<ServiceFulfilmentMetricDto> {
    return this.http
      .post<ServiceFulfilmentMetricResponse>(`${this.apiUrl}/metrics`, request)
      .pipe(
        map((item) => ({
          id: item.id,
          kpi: item.kpi,
          target: item.target,
          platform: item.platform,
          responsibleDgm: item.responsibleDgm,
          definedoladetails: item.definedoladetails ?? (item as any).defineDoladetails,
          weightage: item.weightage,
          area: item.area,
          kpiValue: item.kpi_value,
          month: item.month,
          year: item.year
        }))
      );
  }
}
