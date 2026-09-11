import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './create-quote/auth.service';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  // Back to home (not straight to /login) so the visitor sees both options —
  // sign up or log in — with a reason, instead of landing on a bare form.
  return auth.isAuthenticated()
    ? true
    : router.createUrlTree(['/'], { queryParams: { reason: 'unauthorized' } });
};
