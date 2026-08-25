import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AdminAuthServices } from '../services/admin-auth-services';

export function adminPermissionGuard(permission: string): CanActivateFn {
  return (_route, state) => {
    const auth = inject(AdminAuthServices);
    const router = inject(Router);

    if (auth.hasPermission(permission)) return true;

    // '/admin' is where every failed guard redirects to — if this guard is the
    // one protecting '/admin' itself (the dashboard route), redirecting there
    // again would loop forever. Cancel the navigation instead of looping.
    if (state.url === '/admin') return false;

    return router.createUrlTree(['/admin']);
  };
}
