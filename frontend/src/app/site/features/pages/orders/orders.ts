import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AccountServices } from '../../../core/services/account-services';
import { OrderSummaryInterface } from '../../../shared/interface/account-interfaces';

@Component({
  selector: 'app-orders',
  imports: [RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './orders.html',
  styleUrl: './orders.scss',
})
export class OrdersComponent {
  private accountService = inject(AccountServices);
  orders = signal<OrderSummaryInterface[]>([]);
  loading = signal(true);

  constructor() {
    this.accountService.getOrders().subscribe({
      next: data => {
        this.orders.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  statusClass(status: string): string {
    return `status-badge status-${status.toLowerCase()}`;
  }
}
