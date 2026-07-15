import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { ProductServices } from '../../../core/services/product-services';
import { CartServices } from '../../../core/services/cart-services';
import { AccountServices } from '../../../core/services/account-services';
import { ProductDetailsInterface } from '../../../shared/interface/productDetailsInterface';
import { ProductInterface } from '../../../shared/interface/productInterface';

@Component({
  selector: 'app-product-details',
  imports: [RouterLink, CurrencyPipe, DatePipe],
  templateUrl: './product-details.html',
  styleUrl: './product-details.scss',
})
export class ProductDetailsComponent {
  private productServices = inject(ProductServices);
  private cart = inject(CartServices);
  protected account = inject(AccountServices);
  private router = inject(Router);

  // Bound from the /products/:slug route via withComponentInputBinding().
  slug = input.required<string>();

  product = signal<ProductDetailsInterface | null>(null);
  related = signal<ProductInterface[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  selectedImage = signal<string | null>(null);
  quantity = signal(1);

  gallery = computed<string[]>(() => {
    const p = this.product();
    if (!p) return [];
    if (p.images?.length) return p.images;
    return p.image ? [p.image] : [];
  });

  displayPrice = computed(() => {
    const p = this.product();
    return p?.priceAfterSale ?? p?.price ?? 0;
  });

  discountPercent = computed(() => {
    const p = this.product();
    if (!p) return 0;
    if (p.sale) return p.sale;
    if (p.priceAfterSale && p.price > p.priceAfterSale) {
      return Math.round(((p.price - p.priceAfterSale) / p.price) * 100);
    }
    return 0;
  });

  inStock = computed(() => (this.product()?.stockQuantity ?? 0) > 0);

  constructor() {
    this.account.ensureFavoritesLoaded();

    // Re-fetch whenever the route slug changes.
    effect(() => {
      const slug = this.slug();
      if (slug) this.fetchProduct(slug);
    });
  }

  private fetchProduct(slug: string): void {
    this.loading.set(true);
    this.error.set(null);
    this.productServices.getProductBySlug(slug).subscribe({
      next: (product) => {
        this.product.set(product);
        this.selectedImage.set(product.images?.[0] ?? product.image ?? null);
        this.quantity.set(1);
        this.loading.set(false);
        this.loadRelated(product.categoryId, product.slug);
      },
      error: () => {
        this.error.set("Sorry, we couldn't load this product.");
        this.loading.set(false);
      },
    });
  }

  private loadRelated(categoryId: number, currentSlug: string): void {
    this.productServices.getProducts().subscribe({
      next: (list) => {
        this.related.set(
          list.filter((p) => p.categoryId === categoryId && p.slug !== currentSlug).slice(0, 4)
        );
      },
      error: () => this.related.set([]),
    });
  }

  selectImage(image: string): void {
    this.selectedImage.set(image);
  }

  increment(): void {
    this.quantity.update((q) => q + 1);
  }

  decrement(): void {
    this.quantity.update((q) => Math.max(1, q - 1));
  }

  addToCart(): void {
    const p = this.product();
    if (!p) return;
    this.cart.add(
      {
        id: p.id,
        title: p.title,
        slug: p.slug,
        image: p.images?.[0] ?? p.image,
        price: p.priceAfterSale ?? p.price,
        originalPrice: p.priceAfterSale ? p.price : undefined,
      },
      this.quantity()
    );
  }

  toggleFavorite(): void {
    const p = this.product();
    if (!p) return;

    if (!this.account.isLoggedIn()) {
      this.router.navigate(['/auth/login'], { queryParams: { returnUrl: `/products/${p.slug}` } });
      return;
    }

    this.account.toggleFavorite(p.id);
  }
}
