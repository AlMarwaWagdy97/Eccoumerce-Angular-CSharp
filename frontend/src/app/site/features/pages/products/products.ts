import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ProductCardComponent } from '../../../shared/component/product-card-component/product-card-component';
import { ProductServices } from '../../../core/services/product-services';
import { CategoryServices } from '../../../core/services/category-services';
import { ProductInterface } from '../../../shared/interface/productInterface';
import { CategoryInterface } from '../../../shared/interface/categoryInterface';

type SortOption = 'featured' | 'price-asc' | 'price-desc' | 'newest';

@Component({
  selector: 'app-products',
  imports: [ProductCardComponent],
  templateUrl: './products.html',
  styleUrl: './products.scss',
})
export class ProductsComponent implements OnInit {
  private productServices = inject(ProductServices);
  private categoryServices = inject(CategoryServices);

  products = signal<ProductInterface[]>([]);
  categories = signal<CategoryInterface[]>([]);
  loading = signal(true);

  selectedCategoryId = signal<number | null>(null);
  minPrice = signal<number | null>(null);
  maxPrice = signal<number | null>(null);
  sort = signal<SortOption>('featured');

  private categoryCounts = computed(() => {
    const counts = new Map<number, number>();
    for (const p of this.products()) {
      counts.set(p.categoryId, (counts.get(p.categoryId) ?? 0) + 1);
    }
    return counts;
  });

  filtered = computed(() => {
    let list = [...this.products()];

    const catId = this.selectedCategoryId();
    if (catId !== null) {
      list = list.filter((p) => p.categoryId === catId);
    }

    const min = this.minPrice();
    const max = this.maxPrice();
    const priceOf = (p: ProductInterface) => p.priceAfterSale ?? p.price;
    if (min !== null) list = list.filter((p) => priceOf(p) >= min);
    if (max !== null) list = list.filter((p) => priceOf(p) <= max);

    switch (this.sort()) {
      case 'price-asc':
        list.sort((a, b) => priceOf(a) - priceOf(b));
        break;
      case 'price-desc':
        list.sort((a, b) => priceOf(b) - priceOf(a));
        break;
      case 'newest':
        list.sort((a, b) => (b.sort ?? 0) - (a.sort ?? 0));
        break;
    }
    return list;
  });

  ngOnInit(): void {
    this.productServices.getProducts().subscribe({
      next: (products) => {
        this.products.set(products);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
    this.categoryServices.getCategories().subscribe((categories) => this.categories.set(categories));
  }

  countFor(id: number): number {
    return this.categoryCounts().get(id) ?? 0;
  }

  selectCategory(id: number | null): void {
    this.selectedCategoryId.set(id);
  }

  onMinPrice(value: string): void {
    this.minPrice.set(value ? +value : null);
  }

  onMaxPrice(value: string): void {
    this.maxPrice.set(value ? +value : null);
  }

  onSort(value: string): void {
    this.sort.set(value as SortOption);
  }
}
