import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AgedNetworkFailureMetric {
  id: number;
  areaCode: string;
  percentage: number;
  remarks: string;
  month: number;
  year: number;
}

export interface UpsertAgedNetworkFailureMetric {
  areaCode: string;
  percentage: number;
  remarks: string;
  month: number;
  year: number;
}


@Injectable({ providedIn: 'root' })
export class AgedNetworkFailureService {
  private readonly base = `${environment.apiUrl}/aged-network-failure-metrics`;

  constructor(private http: HttpClient) {}

  get(areaCode: string, month: number, year: number): Observable<AgedNetworkFailureMetric[]> {
    const params = new HttpParams()
      .set('areaCode', areaCode)
      .set('month', month)
      .set('year', year);

    return this.http.get<AgedNetworkFailureMetric[]>(this.base, { params });
  }

  upsert(dto: UpsertAgedNetworkFailureMetric): Observable<AgedNetworkFailureMetric> {
    return this.http.post<AgedNetworkFailureMetric>(this.base, dto);
  }

}
