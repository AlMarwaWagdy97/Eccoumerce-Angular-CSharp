import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { DashboardReportsInterface, DashboardSummaryInterface } from '../../shared/interface/dashboardInterface';
import { AdminApiEnvelope } from '../../shared/interface/admin-auth-interfaces';

@Injectable({ providedIn: 'root' })
export class DashboardServices {
  private http = inject(HttpClient);

  getSummary(): Observable<DashboardSummaryInterface> {
    return this.http.get<AdminApiEnvelope<DashboardSummaryInterface>>('/Admin/Dashboard/summary').pipe(map(response => response.data));
  }

  getReports(): Observable<DashboardReportsInterface> {
    return this.http.get<AdminApiEnvelope<DashboardReportsInterface>>('/Admin/Dashboard/reports').pipe(map(response => response.data));
  }
}
