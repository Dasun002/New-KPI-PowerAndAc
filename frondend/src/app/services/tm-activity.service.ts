/* File: tm-activity.service.ts
   Description: TM Activity Plans service
   Purpose: Manages TM (Telecom Management) Activity Plan CRUD operations.
*/

import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

/* ========== DATA INTERFACES ========== */

/* Activity plan record */
export interface ActivityRecord {
  /* Unique activity plan identifier */
  id?: number;
  /* Record number */
  no: string;
  /* KPI name */
  kpi: string;
  /* Target value */
  target: string;
  /* Calculation method */
  calculation: string;
}

/* ========== TM ACTIVITY SERVICE ========== */

@Injectable({
  providedIn: 'root'
})
export class TmActivityService {
  /* Backend API base URL */
  private apiBase = `${environment.apiUrl}/TmActivityPlans`;

  // Injects the required dependencies.
  constructor(private http: HttpClient) {}

  // Retrieves all TM activity plans
  getAll(): Observable<ActivityRecord[]> {
    return this.http.get<ActivityRecord[]>(this.apiBase);
  }

  // Creates a new TM activity plan
  add(data: Omit<ActivityRecord, 'id'>): Observable<ActivityRecord> {
    return this.http.post<ActivityRecord>(this.apiBase, data);
  }

  // Updates an existing TM activity plan by ID
  update(id: number, data: Partial<ActivityRecord>): Observable<ActivityRecord> {
    return this.http.put<ActivityRecord>(`${this.apiBase}/${id}`, data);
  }

  // Deletes a TM activity plan by its ID
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiBase}/${id}`);
  }
}
