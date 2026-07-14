import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AccountServices } from '../../../core/services/account-services';
import { ProfileResponseInterface } from '../../../shared/interface/account-interfaces';

@Component({
  selector: 'app-profile',
  imports: [ReactiveFormsModule],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
})
export class ProfileComponent {
  private accountService = inject(AccountServices);
  private fb = inject(FormBuilder);

  profile = signal<ProfileResponseInterface | null>(null);
  loading = signal(true);
  saving = signal(false);
  saved = signal(false);
  error = signal('');

  form = this.fb.nonNullable.group({
    firstName: ['', Validators.required],
    lastName: ['', Validators.required],
    phoneNumber: [''],
  });

  constructor() {
    this.accountService.getProfile().subscribe({
      next: data => {
        this.profile.set(data);
        this.form.patchValue({
          firstName: data.firstName,
          lastName: data.lastName,
          phoneNumber: data.phoneNumber ?? '',
        });
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  get initial(): string {
    const name = this.profile()?.firstName ?? '';
    return name.charAt(0).toUpperCase() || '?';
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.saved.set(false);
    this.error.set('');

    const { firstName, lastName, phoneNumber } = this.form.getRawValue();

    this.accountService.updateProfile({ firstName, lastName, phoneNumber: phoneNumber || undefined }).subscribe({
      next: data => {
        this.profile.set(data);
        this.saving.set(false);
        this.saved.set(true);
        setTimeout(() => this.saved.set(false), 2500);
      },
      error: () => {
        this.saving.set(false);
        this.error.set('Could not save your changes. Please try again.');
      },
    });
  }
}
