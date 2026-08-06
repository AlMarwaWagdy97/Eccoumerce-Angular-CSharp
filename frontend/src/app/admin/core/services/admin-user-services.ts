import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { AdminUserInterface, CreateAdminUserRequest, UpdateAdminUserRequest } from '../../shared/interface/admin-user-interfaces';
import { AdminApiEnvelope } from '../../shared/interface/admin-auth-interfaces';

@Injectable({ providedIn: 'root' })
export class AdminUserServices {
  private http = inject(HttpClient);

  getAdmins(): Observable<AdminUserInterface[]> {
    return this.http.get<AdminApiEnvelope<AdminUserInterface[]>>('/Admin/Admins').pipe(map(response => response.data));
  }

  createAdmin(request: CreateAdminUserRequest): Observable<AdminUserInterface> {
    return this.http.post<AdminApiEnvelope<AdminUserInterface>>('/Admin/Admins', request).pipe(map(response => response.data));
  }

  updateAdmin(id: number, request: UpdateAdminUserRequest): Observable<AdminUserInterface> {
    return this.http.put<AdminApiEnvelope<AdminUserInterface>>(`/Admin/Admins/${id}`, request).pipe(map(response => response.data));
  }

  setAdminStatus(id: number, isActive: boolean): Observable<void> {
    return this.http.put<AdminApiEnvelope<unknown>>(`/Admin/Admins/${id}/status`, { isActive }).pipe(map(() => undefined));
  }

  deleteAdmin(id: number): Observable<void> {
    return this.http.delete<AdminApiEnvelope<unknown>>(`/Admin/Admins/${id}`).pipe(map(() => undefined));
  }
}
