import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { AccountServices } from '../../../core/services/account-services';

@Component({
  selector: 'app-login',
  imports: [ReactiveFormsModule, RouterLink, CommonModule],
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class LoginComponent {
  private fb = inject(FormBuilder);
  private accountService = inject(AccountServices);
  private router = inject(Router);

  error = signal('');
  loading = signal(false);

  form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.accountService.login(this.form.getRawValue()).subscribe({
      next: () => this.router.navigateByUrl('/profile'),
      error: () => {
        this.error.set('Invalid credentials. Please try again.');
        this.loading.set(false);
      }
    });
  }
}
