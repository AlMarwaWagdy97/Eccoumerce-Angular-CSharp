import { Injectable, PLATFORM_ID, computed, effect, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { CartItemInterface } from '../../shared/interface/cartItemInterface';

const STORAGE_KEY = 'shopdemo_cart';
const FREE_SHIPPING_THRESHOLD = 50;
const SHIPPING_FEE = 5.99;

@Injectable({
  providedIn: 'root',
})
export class CartServices {
  private platformId = inject(PLATFORM_ID);
  private isBrowser = isPlatformBrowser(this.platformId);

  private _items = signal<CartItemInterface[]>(this.load());
  readonly items = this._items.asReadonly();

  readonly count = computed(() => this._items().reduce((sum, i) => sum + i.quantity, 0));

  // Sum of original prices (before sale discounts).
  readonly subtotal = computed(() =>
    this._items().reduce((sum, i) => sum + (i.originalPrice ?? i.price) * i.quantity, 0)
  );

  // Total savings from sale prices.
  readonly discount = computed(() =>
    this._items().reduce((sum, i) => sum + ((i.originalPrice ?? i.price) - i.price) * i.quantity, 0)
  );

  // Sum of effective (after-sale) prices.
  private readonly itemsTotal = computed(() =>
    this._items().reduce((sum, i) => sum + i.price * i.quantity, 0)
  );

  readonly shipping = computed(() => {
    const total = this.itemsTotal();
    return total > 0 && total < FREE_SHIPPING_THRESHOLD ? SHIPPING_FEE : 0;
  });

  readonly total = computed(() => this.itemsTotal() + this.shipping());

  constructor() {
    // Persist to localStorage on every change (browser only).
    effect(() => {
      const items = this._items();
      if (this.isBrowser) {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(items));
      }
    });
  }

  private load(): CartItemInterface[] {
    if (!this.isBrowser) return [];
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? (JSON.parse(raw) as CartItemInterface[]) : [];
    } catch {
      return [];
    }
  }

  add(item: Omit<CartItemInterface, 'quantity'>, quantity = 1): void {
    this._items.update((items) => {
      const existing = items.find((i) => i.id === item.id);
      if (existing) {
        return items.map((i) =>
          i.id === item.id ? { ...i, quantity: i.quantity + quantity } : i
        );
      }
      return [...items, { ...item, quantity }];
    });
  }

  increment(id: number): void {
    this._items.update((items) =>
      items.map((i) => (i.id === id ? { ...i, quantity: i.quantity + 1 } : i))
    );
  }

  decrement(id: number): void {
    this._items.update((items) =>
      items.flatMap((i) => {
        if (i.id !== id) return [i];
        const quantity = i.quantity - 1;
        return quantity <= 0 ? [] : [{ ...i, quantity }];
      })
    );
  }

  remove(id: number): void {
    this._items.update((items) => items.filter((i) => i.id !== id));
  }

  clear(): void {
    this._items.set([]);
  }
}
