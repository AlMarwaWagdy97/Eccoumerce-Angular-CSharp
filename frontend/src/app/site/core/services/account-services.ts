import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, map, Observable, of } from 'rxjs';
import {
  AddressInterface,
  AddressRequest,
  ApiEnvelope,
  AuthResponseInterface,
  CardInterface,
  CardRequest,
  CreateOrderRequest,
  FavoriteInterface,
  OrderDetailInterface,
  OrderSummaryInterface,
  OrderTrackingInterface,
  ProfileResponseInterface,
  UpdateProfileRequest,
} from '../../shared/interface/account-interfaces';

@Injectable({ providedIn: 'root' })
export class AccountServices {
  private http = inject(HttpClient);
  private readonly storageKey = 'shopdemo_auth';

  readonly user = signal<AuthResponseInterface | null>(this.readStoredUser());
  readonly isLoggedIn = computed(() => this.user() !== null);

  // Cache of favorited product IDs so product cards/detail pages can show
  // favorite state without an extra request per card.
  readonly favoriteProductIds = signal<Set<number>>(new Set());
  private favoritesLoaded = false;

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
    const current = this.user();
    this.clearSession();

    if (!current) return;

    // Best-effort: revoke the refresh token server-side. The client-side
    // session is already cleared above regardless of whether this succeeds.
    this.http.post('/Auth/logout', {}).pipe(catchError(() => of(null))).subscribe();
  }

  refreshToken(): Observable<AuthResponseInterface | null> {
    const current = this.user();
    if (!current) return of(null);

    return this.http.post<ApiEnvelope<AuthResponseInterface>>('/Auth/refresh', {
      token: current.token,
      refreshToken: current.refreshToken,
    }).pipe(
      map(response => this.storeUser(response.data)),
      catchError(() => {
        this.clearSession();
        return of(null);
      })
    );
  }

  clearSession(): void {
    localStorage.removeItem(this.storageKey);
    this.user.set(null);
    this.favoriteProductIds.set(new Set());
    this.favoritesLoaded = false;
  }

  // ----- Profile -----

  getProfile(): Observable<ProfileResponseInterface> {
    return this.http.get<ApiEnvelope<ProfileResponseInterface>>('/Profile').pipe(map(response => response.data));
  }

  updateProfile(request: UpdateProfileRequest): Observable<ProfileResponseInterface> {
    return this.http.put<ApiEnvelope<ProfileResponseInterface>>('/Profile', request).pipe(map(response => response.data));
  }

  // ----- Orders -----

  getOrders(): Observable<OrderSummaryInterface[]> {
    return this.http.get<ApiEnvelope<OrderSummaryInterface[]>>('/Orders').pipe(map(response => response.data));
  }

  getOrder(orderNumber: string): Observable<OrderDetailInterface> {
    return this.http.get<ApiEnvelope<OrderDetailInterface>>(`/Orders/${orderNumber}`).pipe(map(response => response.data));
  }

  getOrderTracking(orderNumber: string): Observable<OrderTrackingInterface> {
    return this.http.get<ApiEnvelope<OrderTrackingInterface>>(`/Orders/${orderNumber}/tracking`).pipe(map(response => response.data));
  }

  createOrder(request: CreateOrderRequest): Observable<OrderDetailInterface> {
    return this.http.post<ApiEnvelope<OrderDetailInterface>>('/Orders', request).pipe(map(response => response.data));
  }

  // ----- Favorites -----

  getFavorites(): Observable<FavoriteInterface[]> {
    return this.http.get<ApiEnvelope<FavoriteInterface[]>>('/Favorites').pipe(map(response => response.data));
  }

  addFavorite(productId: number): Observable<void> {
    return this.http.post<ApiEnvelope<unknown>>(`/Favorites/${productId}`, {}).pipe(map(() => undefined));
  }

  removeFavorite(productId: number): Observable<void> {
    return this.http.delete<ApiEnvelope<unknown>>(`/Favorites/${productId}`).pipe(map(() => undefined));
  }

  // Populates favoriteProductIds once per session so product cards can show
  // filled/outline heart state; safe to call from every card's constructor.
  ensureFavoritesLoaded(): void {
    if (!this.isLoggedIn() || this.favoritesLoaded) return;
    this.favoritesLoaded = true;

    this.getFavorites().subscribe({
      next: items => this.favoriteProductIds.set(new Set(items.map(i => i.productId))),
      error: () => { this.favoritesLoaded = false; },
    });
  }

  isFavorite(productId: number): boolean {
    return this.favoriteProductIds().has(productId);
  }

  toggleFavorite(productId: number): void {
    if (!this.isLoggedIn()) return;

    const wasFavorite = this.isFavorite(productId);
    const request$ = wasFavorite ? this.removeFavorite(productId) : this.addFavorite(productId);

    request$.subscribe({
      next: () => {
        this.favoriteProductIds.update(ids => {
          const next = new Set(ids);
          wasFavorite ? next.delete(productId) : next.add(productId);
          return next;
        });
      },
    });
  }

  // ----- Addresses -----

  getAddresses(): Observable<AddressInterface[]> {
    return this.http.get<ApiEnvelope<AddressInterface[]>>('/Addresses').pipe(map(response => response.data));
  }

  addAddress(request: AddressRequest): Observable<AddressInterface> {
    return this.http.post<ApiEnvelope<AddressInterface>>('/Addresses', request).pipe(map(response => response.data));
  }

  updateAddress(id: number, request: AddressRequest): Observable<AddressInterface> {
    return this.http.put<ApiEnvelope<AddressInterface>>(`/Addresses/${id}`, request).pipe(map(response => response.data));
  }

  deleteAddress(id: number): Observable<void> {
    return this.http.delete<ApiEnvelope<unknown>>(`/Addresses/${id}`).pipe(map(() => undefined));
  }

  setDefaultAddress(id: number): Observable<void> {
    return this.http.put<ApiEnvelope<unknown>>(`/Addresses/${id}/default`, {}).pipe(map(() => undefined));
  }

  // ----- Cards -----

  getCards(): Observable<CardInterface[]> {
    return this.http.get<ApiEnvelope<CardInterface[]>>('/Cards').pipe(map(response => response.data));
  }

  addCard(request: CardRequest): Observable<CardInterface> {
    return this.http.post<ApiEnvelope<CardInterface>>('/Cards', request).pipe(map(response => response.data));
  }

  deleteCard(id: number): Observable<void> {
    return this.http.delete<ApiEnvelope<unknown>>(`/Cards/${id}`).pipe(map(() => undefined));
  }

  setDefaultCard(id: number): Observable<void> {
    return this.http.put<ApiEnvelope<unknown>>(`/Cards/${id}/default`, {}).pipe(map(() => undefined));
  }

  // ----- Session storage -----

  private storeUser(payload: AuthResponseInterface | undefined | null): AuthResponseInterface | null {
    if (!payload) {
      this.user.set(null);
      return null;
    }

    localStorage.setItem(this.storageKey, JSON.stringify(payload));
    this.user.set(payload);
    this.favoritesLoaded = false;
    return payload;
  }

  private readStoredUser(): AuthResponseInterface | null {
    if (typeof window === 'undefined') {
      return null;
    }

    try {
      const stored = localStorage.getItem(this.storageKey);
      return stored ? JSON.parse(stored) as AuthResponseInterface : null;
    } catch {
      return null;
    }
  }
}
