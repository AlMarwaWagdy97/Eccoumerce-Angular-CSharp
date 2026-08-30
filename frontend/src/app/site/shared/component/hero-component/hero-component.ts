import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { SliderServices } from '../../../core/services/slider-services';
import { SliderInterface } from '../../interface/sliderInterface';
import { ResolveImageUrlPipe } from '../../pipes/resolve-image-url.pipe';

interface HeroSlide {
  id: number;
  title: string;
  imageUrl: string;
  link?: string | null;
}

// Shown whenever the API has no active/in-schedule sliders, so the homepage
// top is never empty.
const FALLBACK_SLIDE: HeroSlide = {
  id: -1,
  title: 'Tech Deals',
  imageUrl: 'assets/hero-bg.jpg',
  link: null,
};

@Component({
  selector: 'app-hero',
  imports: [],
  templateUrl: './hero-component.html',
  styleUrl: './hero-component.scss',
})
export class HeroComponent implements OnInit {
  private sliderServices = inject(SliderServices);
  // Not a template pipe here — resolved once per slide when the data loads.
  private resolveImageUrl = new ResolveImageUrlPipe();

  private sliders = signal<SliderInterface[]>([]);

  slides = computed<HeroSlide[]>(() => {
    const active = this.sliders();
    if (active.length === 0) return [FALLBACK_SLIDE];

    return active.map(slider => ({
      id: slider.id,
      title: slider.title,
      imageUrl: this.resolveImageUrl.transform(slider.image),
      link: slider.link,
    }));
  });

  ngOnInit(): void {
    this.sliderServices.getSliders().subscribe(sliders => this.sliders.set(sliders));
  }
}
