import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AccountServices } from '../services/account-services';

export const authGuard: CanActivateFn = (_route, state) => {
  const account = inject(AccountServices);
  const router = inject(Router);

  if (account.isLoggedIn()) {
    return true;
  }

  return router.createUrlTree(['/auth/login'], { queryParams: { returnUrl: state.url } });
};
