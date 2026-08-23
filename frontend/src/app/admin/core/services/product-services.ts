import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import {
  AdminProductDetailInterface,
  AdminProductImageInterface,
  AdminProductInterface,
  ProductsPageInterface,
} from '../../shared/interface/productInterface';
import { AdminApiEnvelope } from '../../shared/interface/admin-auth-interfaces';

@Injectable({ providedIn: 'root' })
export class ProductServices {
  private http = inject(HttpClient);

  getProducts(search: string, page: number, pageSize: number): Observable<ProductsPageInterface> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) params = params.set('search', search);

    return this.http.get<AdminApiEnvelope<ProductsPageInterface>>('/Admin/Products', { params }).pipe(map(response => response.data));
  }

  getProduct(id: number): Observable<AdminProductDetailInterface> {
    return this.http.get<AdminApiEnvelope<AdminProductDetailInterface>>(`/Admin/Products/${id}`).pipe(map(response => response.data));
  }

  // Products are posted as multipart/form-data because the request carries an
  // optional ImageFile. Do not set Content-Type — the browser adds the boundary.
  createProduct(payload: FormData): Observable<AdminProductInterface> {
    return this.http.post<AdminApiEnvelope<AdminProductInterface>>('/Admin/Products', payload).pipe(map(response => response.data));
  }

  updateProduct(id: number, payload: FormData): Observable<AdminProductInterface> {
    return this.http.put<AdminApiEnvelope<AdminProductInterface>>(`/Admin/Products/${id}`, payload).pipe(map(response => response.data));
  }

  toggleStatus(id: number): Observable<void> {
    return this.http.put<AdminApiEnvelope<unknown>>(`/Admin/Products/${id}/toggleStatus`, {}).pipe(map(() => undefined));
  }

  deleteProduct(id: number): Observable<void> {
    return this.http.delete<AdminApiEnvelope<unknown>>(`/Admin/Products/${id}`).pipe(map(() => undefined));
  }

  addImages(id: number, files: File[]): Observable<AdminProductImageInterface[]> {
    const payload = new FormData();
    files.forEach(file => payload.append('imageFiles', file, file.name));
    return this.http.post<AdminApiEnvelope<AdminProductImageInterface[]>>(`/Admin/Products/${id}/images`, payload).pipe(map(response => response.data));
  }

  deleteImage(id: number, imageId: number): Observable<void> {
    return this.http.delete<AdminApiEnvelope<unknown>>(`/Admin/Products/${id}/images/${imageId}`).pipe(map(() => undefined));
  }
}
