import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { AdminCategoryInterface } from '../../shared/interface/categoryInterface';
import { AdminApiEnvelope } from '../../shared/interface/admin-auth-interfaces';

@Injectable({ providedIn: 'root' })
export class CategoryServices {
  private http = inject(HttpClient);

  getCategories(): Observable<AdminCategoryInterface[]> {
    return this.http.get<AdminApiEnvelope<AdminCategoryInterface[]>>('/Admin/Categories').pipe(map(response => response.data));
  }

  getCategory(id: number): Observable<AdminCategoryInterface> {
    return this.http.get<AdminApiEnvelope<AdminCategoryInterface>>(`/Admin/Categories/${id}`).pipe(map(response => response.data));
  }

  // Categories are posted as multipart/form-data because the request carries an
  // optional ImageFile. Do not set Content-Type — the browser adds the boundary.
  createCategory(payload: FormData): Observable<AdminCategoryInterface> {
    return this.http.post<AdminApiEnvelope<AdminCategoryInterface>>('/Admin/Categories', payload).pipe(map(response => response.data));
  }

  updateCategory(id: number, payload: FormData): Observable<AdminCategoryInterface> {
    return this.http.put<AdminApiEnvelope<AdminCategoryInterface>>(`/Admin/Categories/${id}`, payload).pipe(map(response => response.data));
  }

  toggleStatus(id: number): Observable<void> {
    return this.http.put<AdminApiEnvelope<unknown>>(`/Admin/Categories/${id}/toggleStatus`, {}).pipe(map(() => undefined));
  }

  deleteCategory(id: number): Observable<void> {
    return this.http.delete<AdminApiEnvelope<unknown>>(`/Admin/Categories/${id}`).pipe(map(() => undefined));
  }
}
