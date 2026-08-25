import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { DashboardServices } from '../../../core/services/dashboard-services';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';
import { DashboardReportsInterface, DashboardSummaryInterface } from '../../../shared/interface/dashboardInterface';

@Component({
  selector: 'app-admin-dashboard',
  imports: [CurrencyPipe, DatePipe, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class DashboardComponent {
  private dashboardService = inject(DashboardServices);
  private auth = inject(AdminAuthServices);

  summary = signal<DashboardSummaryInterface | null>(null);
  summaryLoading = signal(true);
  summaryError = signal('');

  reports = signal<DashboardReportsInterface | null>(null);
  reportsLoading = signal(false);
  reportsError = signal('');

  canViewReports = () => this.auth.hasPermission('reports.view');

  constructor() {
    this.loadSummary();
    if (this.canViewReports()) {
      this.loadReports();
    }
  }

  private loadSummary(): void {
    this.summaryLoading.set(true);
    this.dashboardService.getSummary().subscribe({
      next: data => {
        this.summary.set(data);
        this.summaryLoading.set(false);
      },
      error: () => {
        this.summaryLoading.set(false);
        this.summaryError.set('Could not load the dashboard summary. Try again.');
      },
    });
  }

  private loadReports(): void {
    this.reportsLoading.set(true);
    this.dashboardService.getReports().subscribe({
      next: data => {
        this.reports.set(data);
        this.reportsLoading.set(false);
      },
      error: () => {
        this.reportsLoading.set(false);
        this.reportsError.set('Could not load reports. Try again.');
      },
    });
  }
}
