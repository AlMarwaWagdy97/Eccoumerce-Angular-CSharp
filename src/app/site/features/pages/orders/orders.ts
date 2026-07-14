import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AccountServices } from '../../../core/services/account-services';

@Component({
  selector: 'app-orders',
  imports: [CommonModule, RouterLink],
  templateUrl: './orders.html',
  styleUrl: './orders.scss',
})
export class OrdersComponent {
  private accountService = inject(AccountServices);
  orders = signal<any[]>([]);
  loading = signal(true);

  constructor() {
    this.accountService.getOrders().subscribe({
      next: data => {
        this.orders.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
