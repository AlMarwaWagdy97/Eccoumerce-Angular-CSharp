import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AccountServices } from '../../../core/services/account-services';
import { AddressInterface } from '../../../shared/interface/account-interfaces';

@Component({
  selector: 'app-address',
  imports: [ReactiveFormsModule],
  templateUrl: './address.html',
  styleUrl: './address.scss',
})
export class AddressComponent {
  private accountService = inject(AccountServices);
  private fb = inject(FormBuilder);

  addresses = signal<AddressInterface[]>([]);
  loading = signal(true);
  saving = signal(false);
  error = signal('');
  showForm = signal(false);
  editingId = signal<number | null>(null);
  busyId = signal<number | null>(null);

  form = this.fb.nonNullable.group({
    fullName: ['', Validators.required],
    phone: ['', Validators.required],
    line1: ['', Validators.required],
    line2: [''],
    city: ['', Validators.required],
    state: ['', Validators.required],
    country: ['', Validators.required],
    postalCode: [''],
    isDefault: [false],
  });

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.accountService.getAddresses().subscribe({
      next: data => {
        this.addresses.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  startAdd(): void {
    this.editingId.set(null);
    this.form.reset({ isDefault: false });
    this.showForm.set(true);
  }

  startEdit(address: AddressInterface): void {
    this.editingId.set(address.id);
    this.form.reset({
      fullName: address.fullName,
      phone: address.phone,
      line1: address.line1,
      line2: address.line2 ?? '',
      city: address.city,
      state: address.state,
      country: address.country,
      postalCode: address.postalCode ?? '',
      isDefault: address.isDefault,
    });
    this.showForm.set(true);
  }

  cancel(): void {
    this.showForm.set(false);
    this.error.set('');
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set('');

    const raw = this.form.getRawValue();
    const request = {
      fullName: raw.fullName,
      phone: raw.phone,
      line1: raw.line1,
      line2: raw.line2 || undefined,
      city: raw.city,
      state: raw.state,
      country: raw.country,
      postalCode: raw.postalCode || undefined,
      isDefault: raw.isDefault,
    };

    const editingId = this.editingId();
    const request$ = editingId
      ? this.accountService.updateAddress(editingId, request)
      : this.accountService.addAddress(request);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.showForm.set(false);
        this.load();
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Could not save this address. Please check the details and try again.');
      },
    });
  }

  remove(address: AddressInterface): void {
    this.busyId.set(address.id);
    this.accountService.deleteAddress(address.id).subscribe({
      next: () => {
        this.addresses.update(items => items.filter(a => a.id !== address.id));
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }

  setDefault(address: AddressInterface): void {
    this.busyId.set(address.id);
    this.accountService.setDefaultAddress(address.id).subscribe({
      next: () => {
        this.load();
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }
}
