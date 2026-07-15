import { Component, inject, input, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { ProductInterface } from '../../interface/productInterface';
import { CartServices } from '../../../core/services/cart-services';
import { AccountServices } from '../../../core/services/account-services';

@Component({
  selector: 'app-product-card',
  imports: [RouterLink, CurrencyPipe],
  templateUrl: './product-card-component.html',
  styleUrl: './product-card-component.scss',
})
export class ProductCardComponent {
  product = input.required<ProductInterface>();

  private cart = inject(CartServices);
  protected account = inject(AccountServices);
  private router = inject(Router);

  // Brief "Added ✓" feedback after a click.
  justAdded = signal(false);

  constructor() {
    this.account.ensureFavoritesLoaded();
  }

  addToCart(event: Event): void {
    // The card is wrapped in a routerLink — don't navigate when adding.
    event.preventDefault();
    event.stopPropagation();

    const p = this.product();
    const onSale = p.priceAfterSale != null && p.priceAfterSale < p.price;

    this.cart.add({
      id: p.id,
      title: p.title,
      slug: p.slug,
      image: p.image,
      price: onSale ? p.priceAfterSale! : p.price,
      originalPrice: onSale ? p.price : undefined,
    });

    this.justAdded.set(true);
    setTimeout(() => this.justAdded.set(false), 1500);
  }

  toggleFavorite(event: Event): void {
    event.preventDefault();
    event.stopPropagation();

    if (!this.account.isLoggedIn()) {
      this.router.navigate(['/auth/login'], { queryParams: { returnUrl: '/products' } });
      return;
    }

    this.account.toggleFavorite(this.product().id);
  }
}
