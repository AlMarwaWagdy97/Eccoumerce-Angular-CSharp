import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ClientServices } from '../../../core/services/client-services';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';
import { ClientDetailInterface, ClientInterface } from '../../../shared/interface/client-interfaces';

@Component({
  selector: 'app-admin-clients',
  imports: [ReactiveFormsModule, CurrencyPipe],
  templateUrl: './clients.html',
  styleUrl: './clients.scss',
})
export class ClientsComponent {
  private clientService = inject(ClientServices);
  private auth = inject(AdminAuthServices);
  private fb = inject(FormBuilder);

  private readonly pageSize = 20;

  clients = signal<ClientInterface[]>([]);
  page = signal(1);
  totalPages = signal(0);
  totalCount = signal(0);
  searchTerm = signal('');

  detail = signal<ClientDetailInterface | null>(null);

  loading = signal(true);
  saving = signal(false);
  error = signal('');
  showForm = signal(false);
  editingId = signal<string | null>(null);
  busyId = signal<string | null>(null);

  canManage = () => this.auth.hasPermission('clients.manage');

  form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    phoneNumber: [''],
  });

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.clientService.getClients(this.searchTerm(), this.page(), this.pageSize).subscribe({
      next: data => {
        this.clients.set(data.items);
        this.totalPages.set(data.totalPages);
        this.totalCount.set(data.totalCount);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onSearchInput(event: Event): void {
    this.searchTerm.set((event.target as HTMLInputElement).value);
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

  view(client: ClientInterface): void {
    this.detail.set(null);
    this.busyId.set(client.id);
    this.clientService.getClient(client.id).subscribe({
      next: data => {
        this.detail.set(data);
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }

  closeDetail(): void {
    this.detail.set(null);
  }

  startEdit(client: ClientInterface): void {
    this.editingId.set(client.id);
    this.form.reset({
      firstName: client.firstName,
      lastName: client.lastName,
      email: client.email,
      phoneNumber: client.phoneNumber ?? '',
    });
    this.showForm.set(true);
  }

  cancel(): void {
    this.showForm.set(false);
    this.error.set('');
  }

  save(): void {
    const editingId = this.editingId();
    if (!editingId) return;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set('');

    const raw = this.form.getRawValue();
    this.clientService.updateClient(editingId, {
      firstName: raw.firstName,
      lastName: raw.lastName,
      email: raw.email,
      phoneNumber: raw.phoneNumber || undefined,
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Could not save this client. Check the email is not already used by another account.');
      },
    });
  }

  toggleStatus(client: ClientInterface): void {
    this.busyId.set(client.id);
    this.clientService.toggleStatus(client.id).subscribe({
      next: () => {
        this.busyId.set(null);
        this.load();
      },
      error: () => this.busyId.set(null),
    });
  }

  remove(client: ClientInterface): void {
    this.busyId.set(client.id);
    this.clientService.deleteClient(client.id).subscribe({
      next: () => {
        this.clients.update(items => items.filter(c => c.id !== client.id));
        this.totalCount.update(count => count - 1);
        this.detail.set(null);
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }
}
