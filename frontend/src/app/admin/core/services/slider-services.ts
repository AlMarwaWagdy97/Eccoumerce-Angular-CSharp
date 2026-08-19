import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { SliderInterface } from '../../shared/interface/slider-interfaces';
import { AdminApiEnvelope } from '../../shared/interface/admin-auth-interfaces';

@Injectable({ providedIn: 'root' })
export class SliderServices {
  private http = inject(HttpClient);

  getSliders(): Observable<SliderInterface[]> {
    return this.http.get<AdminApiEnvelope<SliderInterface[]>>('/Admin/Sliders').pipe(map(response => response.data));
  }

  getSlider(id: number): Observable<SliderInterface> {
    return this.http.get<AdminApiEnvelope<SliderInterface>>(`/Admin/Sliders/${id}`).pipe(map(response => response.data));
  }

  // Sliders are posted as multipart/form-data because the request carries an
  // optional ImageFile. Do not set Content-Type — the browser adds the boundary.
  createSlider(payload: FormData): Observable<SliderInterface> {
    return this.http.post<AdminApiEnvelope<SliderInterface>>('/Admin/Sliders', payload).pipe(map(response => response.data));
  }

  updateSlider(id: number, payload: FormData): Observable<SliderInterface> {
    return this.http.put<AdminApiEnvelope<SliderInterface>>(`/Admin/Sliders/${id}`, payload).pipe(map(response => response.data));
  }

  toggleStatus(id: number): Observable<void> {
    return this.http.put<AdminApiEnvelope<unknown>>(`/Admin/Sliders/${id}/toggleStatus`, {}).pipe(map(() => undefined));
  }

  deleteSlider(id: number): Observable<void> {
    return this.http.delete<AdminApiEnvelope<unknown>>(`/Admin/Sliders/${id}`).pipe(map(() => undefined));
  }
}
