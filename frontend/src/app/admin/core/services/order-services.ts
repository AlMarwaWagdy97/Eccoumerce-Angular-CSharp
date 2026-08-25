import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { AdminOrderDetailInterface, OrdersPageInterface } from '../../shared/interface/orderInterface';
import { AdminApiEnvelope } from '../../shared/interface/admin-auth-interfaces';

@Injectable({ providedIn: 'root' })
export class OrderServices {
  private http = inject(HttpClient);

  getOrders(search: string, status: string, page: number, pageSize: number): Observable<OrdersPageInterface> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    if (status) params = params.set('status', status);

    return this.http.get<AdminApiEnvelope<OrdersPageInterface>>('/Admin/Orders', { params }).pipe(map(response => response.data));
  }

  getOrder(orderNumber: string): Observable<AdminOrderDetailInterface> {
    return this.http.get<AdminApiEnvelope<AdminOrderDetailInterface>>(`/Admin/Orders/${orderNumber}`).pipe(map(response => response.data));
  }

  updateStatus(orderNumber: string, status: string, paymentStatus: string): Observable<AdminOrderDetailInterface> {
    return this.http.put<AdminApiEnvelope<AdminOrderDetailInterface>>(`/Admin/Orders/${orderNumber}/status`, { status, paymentStatus }).pipe(map(response => response.data));
  }
}
