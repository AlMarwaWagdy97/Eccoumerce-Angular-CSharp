import { HttpInterceptorFn } from '@angular/common/http';
import { Environment } from '../environments/environment'; 

export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.url.startsWith('http://') || req.url.startsWith('https://')) {
    return next(req);
  }

  const apiReq = req.clone({
    url: `${Environment.apiUrl}${req.url}`
  });

  return next(apiReq);
};