/* File: region.service.ts
   Description: Region data service
   Purpose: Manages CRUD operations for regional KPI data including
   region information, provinces, and network engineers.
*/

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

/* ========== DATA INTERFACES ========== */

/* Region entity with network engineer assignment */
export interface Region {
  /* Unique region identifier */
  id: number;
  /* Region name */
  region: string;
  /* Province where region is located */
  province: string;
  /* Assigned network engineer */
  networkengineer: string;
  /* LEA (Least Cost Route) code */
  leacode: string;
  /* Engineer Name */
  engName?: string;
}

/* ========== REGION SERVICE ========== */

@Injectable({ providedIn: 'root' })
export class RegionService {
  /* Backend API endpoint */
  //private apiUrl = 'http://localhost:5043/api/regiondata';
  private apiUrl = `${environment.apiUrl}/regiondata`;
  // Injects the required dependencies.
  constructor(private http: HttpClient) {}

  // Retrieves all regions
  getAll(): Observable<Region[]> {
    return this.http.get<Region[]>(this.apiUrl);
  }

  // Creates a new region
  create(data: Omit<Region, 'id'>): Observable<Region> {
    return this.http.post<Region>(this.apiUrl, data);
  }

  // Updates an existing region by ID
  update(id: number, data: Region): Observable<Region> {
    return this.http.put<Region>(`${this.apiUrl}/${id}`, data);
  }

  // Deletes a region by its ID
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
