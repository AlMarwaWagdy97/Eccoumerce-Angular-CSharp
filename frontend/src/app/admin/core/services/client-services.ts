import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { ClientDetailInterface, ClientInterface, ClientsPageInterface, UpdateClientRequest } from '../../shared/interface/client-interfaces';
import { AdminApiEnvelope } from '../../shared/interface/admin-auth-interfaces';

@Injectable({ providedIn: 'root' })
export class ClientServices {
  private http = inject(HttpClient);

  getClients(search: string, page: number, pageSize: number): Observable<ClientsPageInterface> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);

    return this.http.get<AdminApiEnvelope<ClientsPageInterface>>('/Admin/Clients', { params }).pipe(map(response => response.data));
  }

  getClient(id: string): Observable<ClientDetailInterface> {
    return this.http.get<AdminApiEnvelope<ClientDetailInterface>>(`/Admin/Clients/${id}`).pipe(map(response => response.data));
  }

  updateClient(id: string, request: UpdateClientRequest): Observable<ClientInterface> {
    return this.http.put<AdminApiEnvelope<ClientInterface>>(`/Admin/Clients/${id}`, request).pipe(map(response => response.data));
  }

  toggleStatus(id: string): Observable<void> {
    return this.http.put<AdminApiEnvelope<unknown>>(`/Admin/Clients/${id}/toggleStatus`, {}).pipe(map(() => undefined));
  }

  deleteClient(id: string): Observable<void> {
    return this.http.delete<AdminApiEnvelope<unknown>>(`/Admin/Clients/${id}`).pipe(map(() => undefined));
  }
}
