import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AdminAuthServices } from '../services/admin-auth-services';

export const adminAuthGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AdminAuthServices);
  const router = inject(Router);

  if (auth.isLoggedIn()) {
    return true;
  }

  return router.createUrlTree(['/admin/auth/login'], { queryParams: { returnUrl: state.url } });
};
