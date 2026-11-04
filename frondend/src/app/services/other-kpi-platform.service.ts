import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../environments/environment';

export interface OtherKpiDto {
  id?: number | string;
  networkEngineerKpi: string;
  division: string;
  section: string;
  kpiPercent: number;
  displayOrder?: number;
}

export interface OtherMetricDto {
  id?: number | string;
  otherKpiId?: number | string;
  networkEngineerKpi?: string;
  division?: string;
  section?: string;
  kpiPercent?: number;
  site: string;
  totalFaults?: number | null;
  faultsWithinSla?: number | null;
  repeatedFaults?: number | null;
  totalCustomers?: number | null;
  totalClearanceFaults?: number | null;
  clearedWithin4Hrs?: number | null;
  month: number;
  year: number;
}

export interface UpsertOtherMetricRequest {
  otherKpiId: number;
  site: string;
  totalFaults?: number | null;
  faultsWithinSla?: number | null;
  repeatedFaults?: number | null;
  totalCustomers?: number | null;
  totalClearanceFaults?: number | null;
  clearedWithin4Hrs?: number | null;
  month: number;
  year: number;
}

@Injectable({ providedIn: 'root' })
export class OtherKpiPlatformService {
  private readonly apiUrl = `${environment.apiUrl}/OtherKpi`;

  // Injects the required dependencies.
  constructor(private http: HttpClient) { }

  // Retrieves all other KPIs and determines their display order
  getAll(): Observable<OtherKpiDto[]> {
    return this.http.get<OtherKpiDto[]>(this.apiUrl).pipe(
      map((items) =>
        items.map((item, index) => ({
          ...item,
          displayOrder: (item as any).no ?? index + 1
        }))
      )
    );
  }

  // Fetches other metrics for a specific month and year, optionally filtered by site
  getMetrics(month: number, year: number, site?: string): Observable<OtherMetricDto[]> {
    let params = new HttpParams()
      .set('month', month.toString())
      .set('year', year.toString());

    if (site) {
      params = params.set('site', site);
    }

    return this.http.get<OtherMetricDto[]>(`${this.apiUrl}/metrics`, { params });
  }

  // Inserts or updates a metric entry based on the provided request payload
  upsertMetric(request: UpsertOtherMetricRequest): Observable<OtherMetricDto> {
    return this.http.post<OtherMetricDto>(`${this.apiUrl}/metrics`, request);
  }
}
