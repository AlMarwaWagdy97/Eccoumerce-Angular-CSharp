import { HttpClient } from '@angular/common/http';
import { Injectable, Service } from '@angular/core';
import { Observable } from 'rxjs';

// @Service()

@Injectable ({
  providedIn: 'root'
})

export class ProductServices {

    constructor(private http: HttpClient) {}

  getProducts(): Observable<any[]> {
    return this.http.get<any[]>('/products'); 
  }

  getProductById(id: number): Observable<any> {
    return this.http.get<any>(`/products/${id}`);
  }
}
