import { Component, effect, inject, input, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AccountServices } from '../../../core/services/account-services';
import { OrderTrackingInterface } from '../../../shared/interface/account-interfaces';

@Component({
  selector: 'app-tracking',
  imports: [RouterLink, DatePipe],
  templateUrl: './tracking.html',
  styleUrl: './tracking.scss',
})
export class TrackingComponent {
  private accountService = inject(AccountServices);

  orderNumber = input.required<string>();

  tracking = signal<OrderTrackingInterface | null>(null);
  loading = signal(true);
  notFound = signal(false);

  constructor() {
    effect(() => {
      const orderNumber = this.orderNumber();
      this.loading.set(true);
      this.notFound.set(false);

      this.accountService.getOrderTracking(orderNumber).subscribe({
        next: data => {
          this.tracking.set(data);
          this.loading.set(false);
        },
        error: () => {
          this.notFound.set(true);
          this.loading.set(false);
        },
      });
    });
  }
}
