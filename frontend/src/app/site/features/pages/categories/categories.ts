import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CategoryServices } from '../../../core/services/category-services';
import { CategoryInterface } from '../../../shared/interface/categoryInterface';

@Component({
  selector: 'app-categories',
  imports: [RouterLink],
  templateUrl: './categories.html',
  styleUrl: './categories.scss',
})
export class CategoriesComponent implements OnInit {
  private categoryServices = inject(CategoryServices);

  categories = signal<CategoryInterface[]>([]);
  loading = signal(true);

  async ngOnInit() {
    this.categoryServices.getCategories().subscribe({
      next: (categories) => {
        this.categories.set(categories);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
