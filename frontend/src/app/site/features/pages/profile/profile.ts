import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AccountServices } from '../../../core/services/account-services';

@Component({
  selector: 'app-profile',
  imports: [CommonModule, RouterLink],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
})
export class ProfileComponent {
  private accountService = inject(AccountServices);
  profile = signal<any>(null);
  loading = signal(true);

  constructor() {
    this.accountService.getProfile().subscribe({
      next: data => {
        this.profile.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
