import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export interface EnterpriseKpiDto {
  id?: number | string;
  networkEngineerKpi: string;
  division: string;
  kpiPercent: number;
  displayOrder?: number;
}

interface EnterpriseMetricResponse {
  id?: number | string;
  networkEngineerKpi: string;
  division: string;
  target?: string | null;
  kpiPercent: number;
  site: string;
  kpi_value: number;
  month: number;
  year: number;
  isNew?: boolean;
}

export interface EnterpriseMetricDto {
  id?: number | string;
  networkEngineerKpi: string;
  division: string;
  section: string | null;
  target?: string | null;
  kpiPercent: number;
  site: string;
  kpiValue: number;
  month: number;
  year: number;
  isNew?: boolean;
}

export interface UpsertEnterpriseMetricRequest {
  enterpriseKpiId: number;
  site: string;
  kpiValue?: number | null;
  month: number;
  year: number;
  target?: string | null;
}

@Injectable({ providedIn: 'root' })
export class EnterpriseKpiPlatformService {
  private readonly apiUrl = `${environment.apiUrl}/EnterpriseKpi`;

  // Injects the required dependencies.
  constructor(private http: HttpClient) {}

  // Retrieves all enterprise KPIs and calculates their display order
  getAll(): Observable<EnterpriseKpiDto[]> {
    return this.http.get<EnterpriseKpiDto[]>(this.apiUrl).pipe(
      map((items) =>
        items.map((item, index) => ({
          ...item,
          displayOrder: (item as any).no ?? index + 1
        }))
      )
    );
  }

  // Fetches enterprise metrics for a specific month and year, optionally filtered by site
  getMetrics(month: number, year: number, site?: string): Observable<EnterpriseMetricDto[]> {
    let params = new HttpParams()
      .set('month', month.toString())
      .set('year', year.toString());

    if (site) {
      params = params.set('site', site);
    }

    return this.http.get<EnterpriseMetricResponse[]>(`${this.apiUrl}/metrics`, { params }).pipe(
      map((items) =>
        items.map((item) => ({
          id: item.id,
          networkEngineerKpi: item.networkEngineerKpi,
          division: item.division,
          section: item.target ?? null,
          target: item.target,
          kpiPercent: item.kpiPercent,
          site: item.site,
          kpiValue: item.kpi_value,
          month: item.month,
          year: item.year
        }))
      )
    );
  }

  // Creates or updates an enterprise metric based on the provided request payload
  upsertMetric(request: UpsertEnterpriseMetricRequest): Observable<EnterpriseMetricDto> {
    return this.http.post<EnterpriseMetricResponse>(`${this.apiUrl}/metrics`, request).pipe(
      map((item) => ({
        id: item.id,
        networkEngineerKpi: item.networkEngineerKpi,
        division: item.division,
        section: item.target ?? null,
        target: item.target,
        kpiPercent: item.kpiPercent,
        site: item.site,
        kpiValue: item.kpi_value,
        month: item.month,
        year: item.year,
        isNew: item.isNew
      }))
    );
  }
}