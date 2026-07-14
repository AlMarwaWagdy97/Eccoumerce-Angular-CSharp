import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { HeroComponent } from '../../../shared/component/hero-component/hero-component';
import { CategorySliderComponent } from '../../../shared/component/category-slider-component/category-slider-component';
import { ProductCardComponent } from '../../../shared/component/product-card-component/product-card-component';
import { ProductServices } from '../../../core/services/product-services';
import { ProductInterface } from '../../../shared/interface/productInterface';

@Component({
  selector: 'app-home',
  imports: [HeroComponent, CategorySliderComponent, ProductCardComponent, RouterLink],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class HomeComponent implements OnInit {
  private productServices = inject(ProductServices);

  products = signal<ProductInterface[]>([]);

  featured = computed(() => this.products().slice(0, 8));

  newArrivals = computed(() =>
    [...this.products()].sort((a, b) => (b.sort ?? 0) - (a.sort ?? 0)).slice(0, 8)
  );

  ngOnInit(): void {
    this.productServices.getProducts().subscribe((products) => this.products.set(products));
  }
}
