import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { OrderServices } from '../../../core/services/order-services';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';
import { AdminOrderDetailInterface, AdminOrderSummaryInterface } from '../../../shared/interface/orderInterface';

const ORDER_STATUS_PROGRESSION = ['Pending', 'Paid', 'Shipped', 'Delivered'];
const PAYMENT_STATUSES = ['Pending', 'Paid', 'Failed'];

// Mirrors AdminOrdersController/OrderAdminService's IsLegalTransition exactly:
// forward-only among the progression, plus Cancelled from any non-terminal
// status, plus the current status itself (so the <select> always has the
// order's current value as one of its options).
function legalNextOrderStatuses(current: string): string[] {
  if (current === 'Delivered' || current === 'Cancelled') return [current];
  const index = ORDER_STATUS_PROGRESSION.indexOf(current);
  if (index < 0) return [current];
  return [...ORDER_STATUS_PROGRESSION.slice(index), 'Cancelled'];
}

@Component({
  selector: 'app-admin-orders',
  imports: [CurrencyPipe, DatePipe],
  templateUrl: './orders.html',
  styleUrl: './orders.scss',
})
export class Orders {
  private orderService = inject(OrderServices);
  private auth = inject(AdminAuthServices);

  private readonly pageSize = 20;
  private latestViewRequestId = 0;

  readonly paymentStatuses = PAYMENT_STATUSES;
  readonly allOrderStatuses = [...ORDER_STATUS_PROGRESSION, 'Cancelled'];

  orders = signal<AdminOrderSummaryInterface[]>([]);
  page = signal(1);
  totalPages = signal(0);
  totalCount = signal(0);
  searchTerm = signal('');
  statusFilter = signal('');

  loading = signal(true);
  saving = signal(false);
  error = signal('');
  busyOrderNumber = signal<string | null>(null);

  detail = signal<AdminOrderDetailInterface | null>(null);
  statusDraft = signal('');
  paymentStatusDraft = signal('');

  canManage = () => this.auth.hasPermission('orders.manage');

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.orderService.getOrders(this.searchTerm(), this.statusFilter(), this.page(), this.pageSize).subscribe({
      next: data => {
        this.orders.set(data.items);
        this.totalPages.set(data.totalPages);
        this.totalCount.set(data.totalCount);
        this.loading.set(false);
        this.error.set('');
      },
      error: () => {
        this.loading.set(false);
        this.error.set('Could not load orders. Try again.');
      },
    });
  }

  onSearchInput(event: Event): void {
    this.searchTerm.set((event.target as HTMLInputElement).value);
  }

  onStatusFilterChange(event: Event): void {
    this.statusFilter.set((event.target as HTMLSelectElement).value);
  }

  search(): void {
    this.page.set(1);
    this.detail.set(null);
    this.load();
  }

  goToPage(page: number): void {
    if (page < 1 || (this.totalPages() > 0 && page > this.totalPages())) return;
    this.page.set(page);
    this.detail.set(null);
    this.load();
  }

  legalOrderStatuses(): string[] {
    const current = this.detail()?.status;
    return current ? legalNextOrderStatuses(current) : [];
  }

  isTerminal(status: string): boolean {
    return status === 'Delivered' || status === 'Cancelled';
  }

  view(order: AdminOrderSummaryInterface): void {
    this.detail.set(null);
    this.error.set('');
    this.busyOrderNumber.set(order.orderNumber);

    const requestId = ++this.latestViewRequestId;
    this.orderService.getOrder(order.orderNumber).subscribe({
      next: data => {
        if (requestId !== this.latestViewRequestId) return;
        this.detail.set(data);
        this.statusDraft.set(data.status);
        this.paymentStatusDraft.set(data.paymentStatus);
        this.busyOrderNumber.set(null);
      },
      error: () => {
        if (requestId !== this.latestViewRequestId) return;
        this.busyOrderNumber.set(null);
        this.error.set('Could not load this order. Try again.');
      },
    });
  }

  closeDetail(): void {
    this.detail.set(null);
  }

  onStatusDraftChange(event: Event): void {
    this.statusDraft.set((event.target as HTMLSelectElement).value);
  }

  onPaymentStatusDraftChange(event: Event): void {
    this.paymentStatusDraft.set((event.target as HTMLSelectElement).value);
  }

  saveStatus(): void {
    const current = this.detail();
    if (!current) return;

    this.saving.set(true);
    this.error.set('');
    this.orderService.updateStatus(current.orderNumber, this.statusDraft(), this.paymentStatusDraft()).subscribe({
      next: updated => {
        this.detail.set(updated);
        this.statusDraft.set(updated.status);
        this.paymentStatusDraft.set(updated.paymentStatus);
        this.saving.set(false);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Could not update this order. It may already be in a status that cannot move to the one you selected.');
      },
    });
  }
}
