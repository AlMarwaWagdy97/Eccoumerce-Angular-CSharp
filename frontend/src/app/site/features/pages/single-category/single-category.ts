import { Component, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ProductCardComponent } from '../../../shared/component/product-card-component/product-card-component';
import { ProductServices } from '../../../core/services/product-services';
import { CategoryServices } from '../../../core/services/category-services';
import { ProductInterface } from '../../../shared/interface/productInterface';
import { CategoryInterface } from '../../../shared/interface/categoryInterface';

@Component({
  selector: 'app-single-category',
  imports: [RouterLink, ProductCardComponent],
  templateUrl: './single-category.html',
  styleUrl: './single-category.scss',
})
export class SingleCategoryComponent {
  private productServices = inject(ProductServices);
  private categoryServices = inject(CategoryServices);

  // Bound from the /categories/:id route via withComponentInputBinding().
  id = input.required<string>();

  category = signal<CategoryInterface | null>(null);
  products = signal<ProductInterface[]>([]);
  loading = signal(true);

  constructor() {
    effect(() => {
      const id = Number(this.id());
      if (!Number.isNaN(id)) this.load(id);
    });
  }

  private load(id: number): void {
    this.loading.set(true);

    this.categoryServices.getCategoryById(id).subscribe({
      next: (res) => this.category.set((res?.data ?? res) as CategoryInterface),
      error: () => this.category.set(null),
    });

    this.productServices.getProducts().subscribe({
      next: (list) => {
        this.products.set(list.filter((p) => p.categoryId === id));
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
