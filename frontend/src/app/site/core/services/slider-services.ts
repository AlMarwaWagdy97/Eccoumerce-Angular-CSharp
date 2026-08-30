import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { SliderInterface } from '../../shared/interface/sliderInterface';
import { ApiResponseInterface } from '../../shared/interface/apiResponseInterface';

@Injectable({
  providedIn: 'root'
})
export class SliderServices {

  constructor(private http: HttpClient) {}

  // Public endpoint — already filtered to active, in-schedule sliders and
  // sorted server-side, so no date logic needed here.
  getSliders(): Observable<SliderInterface[]> {
    return this.http.get<ApiResponseInterface<SliderInterface>>('/Sliders').pipe(
      map(response => response.data)
    );
  }
}
