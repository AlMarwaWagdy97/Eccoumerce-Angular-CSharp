import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { PermissionInterface, RoleInterface, RoleRequest } from '../../shared/interface/role-interfaces';
import { AdminApiEnvelope } from '../../shared/interface/admin-auth-interfaces';

@Injectable({ providedIn: 'root' })
export class RoleServices {
  private http = inject(HttpClient);

  getRoles(): Observable<RoleInterface[]> {
    return this.http.get<AdminApiEnvelope<RoleInterface[]>>('/Admin/Roles').pipe(map(response => response.data));
  }

  getPermissionCatalog(): Observable<PermissionInterface[]> {
    return this.http.get<AdminApiEnvelope<PermissionInterface[]>>('/Admin/Permissions').pipe(map(response => response.data));
  }

  createRole(request: RoleRequest): Observable<RoleInterface> {
    return this.http.post<AdminApiEnvelope<RoleInterface>>('/Admin/Roles', request).pipe(map(response => response.data));
  }

  updateRole(id: number, request: RoleRequest): Observable<RoleInterface> {
    return this.http.put<AdminApiEnvelope<RoleInterface>>(`/Admin/Roles/${id}`, request).pipe(map(response => response.data));
  }

  deleteRole(id: number): Observable<void> {
    return this.http.delete<AdminApiEnvelope<unknown>>(`/Admin/Roles/${id}`).pipe(map(() => undefined));
  }
}
