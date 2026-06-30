import { HttpClient } from '@angular/common/http';
import { Injectable, Service } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ProductInterface } from '../../shared/interface/productInterface';
import { ApiResponseInterface } from '../../shared/interface/apiResponseInterface';

// @Service()

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

  getProductById(id: number): Observable<any> {
    return this.http.get<any>(`/products/${id}`);
  }
}
