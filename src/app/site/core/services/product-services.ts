import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ProductInterface } from '../../shared/interface/productInterface';
import { ProductDetailsInterface } from '../../shared/interface/productDetailsInterface';
import { ApiResponseInterface } from '../../shared/interface/apiResponseInterface';

@Injectable ({
  providedIn: 'root'
})

export class ProductServices {

  constructor(private http: HttpClient) {}


  getProducts(): Observable<ProductInterface[]> {
    return this.http.get<ApiResponseInterface<ProductInterface>>('/Products').pipe(
      map(response => response.data)
    );
  }

  getProductById(id: number): Observable<ProductDetailsInterface> {
    return this.http.get<any>(`/products/${id}`).pipe(
      // detail endpoint may return the object directly or wrapped in { data }
      map(response => (response?.data ?? response) as ProductDetailsInterface)
    );
  }

  // NOTE: adjust this route to match the real backend slug endpoint if different.
  getProductBySlug(slug: string): Observable<ProductDetailsInterface> {
    return this.http.get<any>(`/products/${slug}`).pipe(
      map(response => (response?.data ?? response) as ProductDetailsInterface)
    );
  }
}
