import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';

@Component({
  selector: 'app-admin-reset-password',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password.html',
  styleUrl: './reset-password.scss',
})
export class ResetPasswordComponent {
  private auth = inject(AdminAuthServices);
  private router = inject(Router);
  private route = inject(ActivatedRoute);
  private fb = inject(FormBuilder);

  submitting = signal(false);
  done = signal(false);
  error = signal('');

  private email = this.route.snapshot.queryParamMap.get('email') ?? '';
  private token = this.route.snapshot.queryParamMap.get('token') ?? '';

  form = this.fb.nonNullable.group({
    newPassword: ['', [Validators.required, Validators.minLength(8)]],
  });

  submit(): void {
    if (this.form.invalid || !this.email || !this.token) {
      this.form.markAllAsTouched();
      this.error.set(!this.email || !this.token ? 'This reset link is missing required information.' : '');
      return;
    }

    this.submitting.set(true);
    this.error.set('');

    this.auth.resetPassword({ email: this.email, token: this.token, newPassword: this.form.getRawValue().newPassword }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.done.set(true);
      },
      error: () => {
        this.submitting.set(false);
        this.error.set('This reset link is invalid or has expired. Request a new one.');
      },
    });
  }
}
