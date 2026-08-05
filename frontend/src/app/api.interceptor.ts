import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, Observable, switchMap, throwError } from 'rxjs';
import { Environment } from '../environments/environment';
import { AccountServices } from './site/core/services/account-services';
import { AdminAuthServices } from './admin/core/services/admin-auth-services';

// Requests to these endpoints must never trigger a refresh-and-retry (it would
// either be pointless — login/register have no session yet — or recurse into
// the refresh call failing on itself).
const AUTH_ENDPOINTS_NO_RETRY = ['/Auth/login', '/Auth/register', '/Auth/refresh'];
const ADMIN_AUTH_ENDPOINTS_NO_RETRY = ['/Admin/Auth/login', '/Admin/Auth/refresh', '/Admin/Auth/forgot-password', '/Admin/Auth/reset-password'];

function readToken(key: string): string | null {
  try {
    if (typeof window === 'undefined') return null;
    const stored = localStorage.getItem(key);
    if (!stored) return null;
    return JSON.parse(stored)?.token ?? null;
  } catch {
    return null;
  }
}

export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  // Pass through absolute URLs (external resources)
  if (req.url.startsWith('http://') || req.url.startsWith('https://')) {
    return next(req);
  }

  const isAdminRequest = req.url.startsWith('/Admin/');
  const account = inject(AccountServices);
  const adminAuth = inject(AdminAuthServices);
  const router = inject(Router);

  const apiUrl = `${Environment.apiUrl}${req.url}`;
  const token = isAdminRequest ? readToken('shopdemo_admin_auth') : readToken('shopdemo_auth');

  const apiReq = token
    ? req.clone({ url: apiUrl, setHeaders: { Authorization: `Bearer ${token}` } })
    : req.clone({ url: apiUrl });

  return next(apiReq).pipe(
    catchError((error: unknown) => {
      const noRetryList = isAdminRequest ? ADMIN_AUTH_ENDPOINTS_NO_RETRY : AUTH_ENDPOINTS_NO_RETRY;
      const canRetry = error instanceof HttpErrorResponse
        && error.status === 401
        && !noRetryList.some(endpoint => req.url.includes(endpoint));

      if (!canRetry) {
        return throwError(() => error);
      }

      // Explicitly typed to the common shape both refreshToken() methods resolve
      // to — without this, TS widens the ternary's Observable<A|null> |
      // Observable<B|null> into an unusable union and loses `.token` below.
      const refresh$: Observable<{ token: string } | null> =
        isAdminRequest ? adminAuth.refreshToken() : account.refreshToken();

      return refresh$.pipe(
        switchMap(refreshed => {
          if (!refreshed) {
            router.navigate([isAdminRequest ? '/admin/auth/login' : '/auth/login']);
            return throwError(() => error);
          }

          const retryReq = req.clone({ url: apiUrl, setHeaders: { Authorization: `Bearer ${refreshed.token}` } });
          return next(retryReq);
        })
      );
    })
  );
};