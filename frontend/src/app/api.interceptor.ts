import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { Environment } from '../environments/environment';
import { AccountServices } from './site/core/services/account-services';

// Requests to these endpoints must never trigger a refresh-and-retry (it would
// either be pointless — login/register have no session yet — or recurse into
// the refresh call failing on itself).
const AUTH_ENDPOINTS_NO_RETRY = ['/Auth/login', '/Auth/register', '/Auth/refresh'];

function readToken(): string | null {
  try {
    if (typeof window === 'undefined') return null;
    const stored = localStorage.getItem('shopdemo_auth');
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

  const account = inject(AccountServices);
  const router = inject(Router);

  const apiUrl = `${Environment.apiUrl}${req.url}`;
  const token = readToken();

  const apiReq = token
    ? req.clone({ url: apiUrl, setHeaders: { Authorization: `Bearer ${token}` } })
    : req.clone({ url: apiUrl });

  return next(apiReq).pipe(
    catchError((error: unknown) => {
      const canRetry = error instanceof HttpErrorResponse
        && error.status === 401
        && !AUTH_ENDPOINTS_NO_RETRY.some(endpoint => req.url.includes(endpoint));

      if (!canRetry) {
        return throwError(() => error);
      }

      return account.refreshToken().pipe(
        switchMap(refreshed => {
          if (!refreshed) {
            router.navigate(['/auth/login']);
            return throwError(() => error);
          }

          const retryReq = req.clone({ url: apiUrl, setHeaders: { Authorization: `Bearer ${refreshed.token}` } });
          return next(retryReq);
        })
      );
    })
  );
};