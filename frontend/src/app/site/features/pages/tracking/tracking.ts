import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AccountServices } from '../../../core/services/account-services';

@Component({
  selector: 'app-tracking',
  imports: [CommonModule, RouterLink],
  templateUrl: './tracking.html',
  styleUrl: './tracking.scss',
})
export class TrackingComponent {
  private accountService = inject(AccountServices);
  private route = inject(ActivatedRoute);
  tracking = signal<any>(null);
  loading = signal(true);

  constructor() {
    const orderNumber = this.route.snapshot.paramMap.get('orderNumber') ?? '';
    this.accountService.getOrderTracking(orderNumber).subscribe({
      next: data => {
        this.tracking.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
