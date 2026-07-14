import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { ApiEnvelope, AuthResponseInterface, FavoriteInterface, OrderSummaryInterface, ProfileResponseInterface } from '../../shared/interface/account-interfaces';

@Injectable({ providedIn: 'root' })
export class AccountServices {
  private http = inject(HttpClient);
  private readonly storageKey = 'shopdemo_auth';

  readonly user = signal<AuthResponseInterface | null>(this.readStoredUser());

  login(payload: { email: string; password: string }): Observable<AuthResponseInterface> {
    return this.http.post<ApiEnvelope<AuthResponseInterface>>('/Auth/login', payload).pipe(
      map(response => this.storeUser(response.data) as AuthResponseInterface)
    );
  }

  register(payload: { email: string; password: string; firstName: string; lastName: string }): Observable<AuthResponseInterface> {
    return this.http.post<ApiEnvelope<AuthResponseInterface>>('/Auth/register', payload).pipe(
      map(response => this.storeUser(response.data) as AuthResponseInterface)
    );
  }

  logout(): void {
    localStorage.removeItem(this.storageKey);
    this.user.set(null);
  }

  getProfile(): Observable<ProfileResponseInterface> {
    return this.http.get<ApiEnvelope<ProfileResponseInterface>>('/Auth/profile').pipe(map(response => response.data));
  }

  getOrders(): Observable<OrderSummaryInterface[]> {
    return this.http.get<ApiEnvelope<OrderSummaryInterface[]>>('/Auth/orders').pipe(map(response => response.data));
  }

  getOrderTracking(orderNumber: string): Observable<{ orderNumber: string; status: string; createdOn: string }> {
    return this.http.get<ApiEnvelope<{ orderNumber: string; status: string; createdOn: string }>>(`/Auth/orders/${orderNumber}/tracking`).pipe(map(response => response.data));
  }

  getFavorites(): Observable<FavoriteInterface[]> {
    return this.http.get<ApiEnvelope<FavoriteInterface[]>>('/Auth/favorites').pipe(map(response => response.data));
  }

  addFavorite(productId: number): Observable<void> {
    return this.http.post<ApiEnvelope<unknown>>(`/Auth/favorites/${productId}`, {}).pipe(map(() => undefined));
  }

  removeFavorite(productId: number): Observable<void> {
    return this.http.delete<ApiEnvelope<unknown>>(`/Auth/favorites/${productId}`).pipe(map(() => undefined));
  }

  private storeUser(payload: AuthResponseInterface | undefined | null): AuthResponseInterface | null {
    if (!payload) {
      this.user.set(null);
      return null;
    }

    localStorage.setItem(this.storageKey, JSON.stringify(payload));
    this.user.set(payload);
    return payload;
  }

  private readStoredUser(): AuthResponseInterface | null {
    if (typeof window === 'undefined') {
      return null;
    }

    const stored = localStorage.getItem(this.storageKey);
    return stored ? JSON.parse(stored) as AuthResponseInterface : null;
  }
}
