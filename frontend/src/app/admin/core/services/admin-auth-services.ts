import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, map, Observable, of } from 'rxjs';
import { AdminApiEnvelope, AdminAuthResponseInterface } from '../../shared/interface/admin-auth-interfaces';

@Injectable({ providedIn: 'root' })
export class AdminAuthServices {
  private http = inject(HttpClient);
  private readonly storageKey = 'shopdemo_admin_auth';

  readonly user = signal<AdminAuthResponseInterface | null>(this.readStoredAdmin());
  readonly isLoggedIn = computed(() => this.user() !== null);

  login(payload: { email: string; password: string }): Observable<AdminAuthResponseInterface> {
    return this.http.post<AdminApiEnvelope<AdminAuthResponseInterface>>('/Admin/Auth/login', payload).pipe(
      map(response => this.storeAdmin(response.data) as AdminAuthResponseInterface)
    );
  }

  logout(): void {
    const current = this.user();
    this.clearSession();

    if (!current) return;

    // Best-effort: revoke the refresh token server-side. The client-side
    // session is already cleared above regardless of whether this succeeds.
    this.http.post('/Admin/Auth/logout', {}).pipe(catchError(() => of(null))).subscribe();
  }

  refreshToken(): Observable<AdminAuthResponseInterface | null> {
    const current = this.user();
    if (!current) return of(null);

    return this.http.post<AdminApiEnvelope<AdminAuthResponseInterface>>('/Admin/Auth/refresh', {
      token: current.token,
      refreshToken: current.refreshToken,
    }).pipe(
      map(response => this.storeAdmin(response.data)),
      catchError(() => {
        this.clearSession();
        return of(null);
      })
    );
  }

  forgotPassword(email: string): Observable<void> {
    return this.http.post<AdminApiEnvelope<unknown>>('/Admin/Auth/forgot-password', { email }).pipe(map(() => undefined));
  }

  resetPassword(payload: { email: string; token: string; newPassword: string }): Observable<void> {
    return this.http.post<AdminApiEnvelope<unknown>>('/Admin/Auth/reset-password', payload).pipe(map(() => undefined));
  }

  hasPermission(key: string): boolean {
    return this.user()?.permissions.includes(key) ?? false;
  }

  clearSession(): void {
    localStorage.removeItem(this.storageKey);
    this.user.set(null);
  }

  private storeAdmin(payload: AdminAuthResponseInterface | undefined | null): AdminAuthResponseInterface | null {
    if (!payload) {
      this.user.set(null);
      return null;
    }

    localStorage.setItem(this.storageKey, JSON.stringify(payload));
    this.user.set(payload);
    return payload;
  }

  private readStoredAdmin(): AdminAuthResponseInterface | null {
    if (typeof window === 'undefined') {
      return null;
    }

    const stored = localStorage.getItem(this.storageKey);
    return stored ? JSON.parse(stored) as AdminAuthResponseInterface : null;
  }
}
