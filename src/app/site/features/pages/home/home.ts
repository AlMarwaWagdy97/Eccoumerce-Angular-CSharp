import { Component } from '@angular/core';
import { HeroComponent } from '../../../shared/component/hero-component/hero-component';
import { CategorySliderComponent } from '../../../shared/component/category-slider-component/category-slider-component';

@Component({
  selector: 'app-home',
  imports: [HeroComponent, CategorySliderComponent],
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class HomeComponent {}
