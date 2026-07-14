import { HttpInterceptorFn } from '@angular/common/http';
import { Environment } from '../environments/environment'; 

export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  // Pass through absolute URLs (external resources)
  if (req.url.startsWith('http://') || req.url.startsWith('https://')) {
    return next(req);
  }

  // Try to read stored auth token (serialized by AccountServices)
  let token: string | null = null;
  try {
    if (typeof window !== 'undefined') {
      const stored = localStorage.getItem('shopdemo_auth');
      if (stored) {
        const parsed = JSON.parse(stored);
        token = parsed?.token ?? null;
      }
    }
  } catch (_e) {
    token = null;
  }

  const apiUrl = `${Environment.apiUrl}${req.url}`;

  const apiReq = token
    ? req.clone({ url: apiUrl, setHeaders: { Authorization: `Bearer ${token}` } })
    : req.clone({ url: apiUrl });

  return next(apiReq);
};