import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AdminAuthServices } from '../services/admin-auth-services';

export function adminPermissionGuard(permission: string): CanActivateFn {
  return () => {
    const auth = inject(AdminAuthServices);
    const router = inject(Router);

    return auth.hasPermission(permission) ? true : router.createUrlTree(['/admin']);
  };
}
