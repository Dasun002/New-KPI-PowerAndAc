import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export type AnalyticsResultApi = {
  kpiDefinitionId: number;
  kpiName?: string;
  areaCode: string;
  achievedKpi: number;
  maximumPointsPerKpi: number;
  pointsAchieved: number;
  overallKpiValuePercent: number;
  year: number;
};

@Injectable({
  providedIn: 'root'
})
export class AnalyticsService {
  private readonly apiBase = `${environment.apiUrl}/analytics`;

  // Injects the required dependencies.
  constructor(private http: HttpClient) {}

  // Retrieves cumulative analytics data for a given year within a specific month range
  getCumulativeAnalytics(year: number, startMonth: number, endMonth: number): Observable<AnalyticsResultApi[]> {
    const params = new HttpParams()
      .set('year', year.toString())
      .set('startMonth', startMonth.toString())
      .set('endMonth', endMonth.toString());

    return this.http.get<AnalyticsResultApi[]>(this.apiBase, { params });
  }

  // Fetches a list of all available years that have analytics data
  getAvailableYears(): Observable<number[]> {
    return this.http.get<number[]>(`${this.apiBase}/years`);
  }

  // Fetches a list of available months for a given year
  getAvailableMonths(year: number): Observable<number[]> {
    const params = new HttpParams().set('year', year.toString());
    return this.http.get<number[]>(`${this.apiBase}/months`, { params });
  }
}
