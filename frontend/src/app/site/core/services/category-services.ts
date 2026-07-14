import { HttpClient } from '@angular/common/http';
import { Injectable, Service } from '@angular/core';
import { Observable } from 'rxjs';
import { CategoryInterface } from '../../shared/interface/categoryInterface';
import { ApiResponseInterface } from '../../shared/interface/apiResponseInterface';
import { map } from 'rxjs/operators';

// @Service()
@Injectable ({
  providedIn: 'root'
})

export class CategoryServices {

constructor(private http: HttpClient) {}

  // getCategories(): Observable<any[]> {
  //   return this.http.get<any[]>('/Categories'); 
  // }

  getCategories(): Observable<CategoryInterface[]> {
    return this.http.get<ApiResponseInterface<CategoryInterface>>('/Categories').pipe(
      map(response => response.data)
    );
  }

  getCategoryById(id: number): Observable<any> {
    return this.http.get<any>(`/Categories/${id}`);
  }
}
