import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface PowerAndACRecord {
  id: number;
  designation: string;
  year: number;
  month: number;
  scheduled: number;
  attended: number;
  cumulative_Sched: number;
  cumulative_Achieved: number;
  isVerified: boolean;
}

@Injectable({ providedIn: 'root' })
export class PowerAndACService {
  private readonly base = `${environment.apiUrl}/powerandac`;

  // Injects the required dependencies.
  constructor(private http: HttpClient) {}

  // Retrieves Power and AC records for a given year
  getByYear(year: number): Observable<PowerAndACRecord[]> {
    const params = new HttpParams().set('year', year);
    return this.http.get<PowerAndACRecord[]>(this.base, { params });
  }

  // Toggles the verification status of a specific Power and AC record
  toggleVerified(id: number): Observable<{ id: number; isVerified: boolean }> {
    return this.http.patch<{ id: number; isVerified: boolean }>(`${this.base}/${id}/toggle-verified`, {});
  }
}
