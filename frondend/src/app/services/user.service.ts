/* File: user.service.ts
   Description: User management service
   Purpose: Manages user CRUD operations and user data synchronization.
*/

import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';

/* ========== DATA INTERFACES ========== */

/* User entity */
export interface User {
  /* Unique user identifier */
  userId: number;
  /* User's service ID (username) */
  serviceId: string;
  /* User's display name */
  name: string;
  /* User's role (Admin, User, etc.) */
  role: string;
  /* Whether user account is active */
  isActive: boolean;
  /* Pages assigned to this user */
  pages: string[];
  /* Page IDs assigned to this user */
  pageIds: number[];
  /* Last login timestamp */
  lastLogin?: string;
  /* Account creation timestamp */
  createdAt: string;
  /* Last update timestamp */
  updatedAt?: string;
}

/* Payload for creating new user */
export interface CreateUserDto {
  /* Service ID (username) */
  serviceId: string;
  /* Display name */
  name: string;
  /* User role */
  role: string;
  /* Account active status */
  isActive: boolean;
  /* Pages to assign */
  pages?: string[];
  /* Page IDs to assign */
  pageIds: number[];
}

/* Payload for updating user */
export interface UpdateUserDto {
  /* Service ID (optional update) */
  serviceId?: string;
  /* Display name (optional update) */
  name?: string;
  /* Role (optional update) */
  role?: string;
  /* Active status (optional update) */
  isActive?: boolean;
  /* Pages (optional update) */
  pages?: string[];
  /* Page IDs (optional update) */
  pageIds?: number[];
}

/* ========== USER SERVICE ========== */

@Injectable({
  providedIn: 'root'
})
export class UserService {
  /* Backend API endpoint */
 // private apiUrl = 'https://socapps.intranet.slt.com.lk/kpi/api/users';
  private readonly apiUrl = `${environment.apiUrl}/users`;
  // Injects the required dependencies.
  constructor(private http: HttpClient) { }

  // Retrieves all registered users
  getAllUsers(): Observable<User[]> {
    return this.http.get<User[]>(this.apiUrl).pipe(
      catchError(this.handleError)
    );
  }

  // Retrieves a specific user by their ID
  getUserById(id: string): Observable<User> {
    return this.http.get<User>(`${this.apiUrl}/${id}`).pipe(
      catchError(this.handleError)
    );
  }

  // Creates a new user based on the provided payload
  createUser(userData: CreateUserDto): Observable<User> {
    return this.http.post<User>(this.apiUrl, userData).pipe(
      catchError(this.handleError)
    );
  }

  // Updates an existing user's details by ID
  updateUser(id: string, userData: UpdateUserDto): Observable<any> {
    return this.http.put(`${this.apiUrl}/${id}`, userData).pipe(
      catchError(this.handleError)
    );
  }

  // Deletes a user by their ID
  deleteUser(id: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${id}`).pipe(
      catchError(this.handleError)
    );
  }

  // Handles error.
  private handleError(error: HttpErrorResponse) {
    let errorMessage = 'An error occurred';
    if (error.error instanceof ErrorEvent) {
      errorMessage = error.error.message;
    } else {
      errorMessage = `Error Code: ${error.status}\nMessage: ${error.message}`;
    }
    return throwError(() => new Error(errorMessage));
  }
}
