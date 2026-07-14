import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AccountServices } from '../../../core/services/account-services';
import { FavoriteInterface } from '../../../shared/interface/account-interfaces';

@Component({
  selector: 'app-favorites',
  imports: [RouterLink, CurrencyPipe],
  templateUrl: './favorites.html',
  styleUrl: './favorites.scss',
})
export class FavoritesComponent {
  private accountService = inject(AccountServices);
  favorites = signal<FavoriteInterface[]>([]);
  loading = signal(true);
  removingId = signal<number | null>(null);

  constructor() {
    this.accountService.getFavorites().subscribe({
      next: data => {
        this.favorites.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  remove(item: FavoriteInterface): void {
    this.removingId.set(item.id);
    this.accountService.removeFavorite(item.productId).subscribe({
      next: () => {
        this.favorites.update(items => items.filter(i => i.id !== item.id));
        this.removingId.set(null);
      },
      error: () => this.removingId.set(null),
    });
  }
}
