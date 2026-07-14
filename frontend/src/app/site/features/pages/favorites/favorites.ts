import { Component, inject, signal } from '@angular/core';
import { CommonModule, CurrencyPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AccountServices } from '../../../core/services/account-services';

@Component({
  selector: 'app-favorites',
  imports: [CommonModule, RouterLink, CurrencyPipe],
  templateUrl: './favorites.html',
  styleUrl: './favorites.scss',
})
export class FavoritesComponent {
  private accountService = inject(AccountServices);
  favorites = signal<any[]>([]);
  loading = signal(true);

  constructor() {
    this.accountService.getFavorites().subscribe({
      next: data => {
        this.favorites.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }
}
