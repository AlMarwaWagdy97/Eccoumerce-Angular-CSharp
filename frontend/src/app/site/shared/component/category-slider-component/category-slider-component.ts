import { Component, ChangeDetectionStrategy, OnInit } from '@angular/core';
import { CategoryInterface } from '../../interface/categoryInterface';
import { CategoryServices } from '../../../core/services/category-services';
import { Observable } from 'rxjs';
import { AsyncPipe, CommonModule } from '@angular/common';
import { ResolveImageUrlPipe } from '../../pipes/resolve-image-url.pipe';

@Component({
  selector: 'app-category-slider',
  imports: [CommonModule, AsyncPipe, ResolveImageUrlPipe],
  templateUrl: './category-slider-component.html',
  styleUrl: './category-slider-component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush // لسه محافظين على الأداء العالي
})
export class CategorySliderComponent implements OnInit{

  categoriesList!: Observable<CategoryInterface[]>;

  constructor(private categoryServices: CategoryServices) {}

  ngOnInit(): void {
    // نربط المتغير بالـ Stream مباشرة بدون subscribe يدوي
    this.categoriesList = this.categoryServices.getCategories();
  }


}
