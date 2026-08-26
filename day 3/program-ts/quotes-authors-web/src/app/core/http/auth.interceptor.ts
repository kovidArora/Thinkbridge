import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { switchMap } from 'rxjs';
import { AuthService } from '../../create-quote/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  // Never attach a token to the login call itself — that request IS how a token gets obtained.
  // Doing this unconditionally would cause AuthService.getAccessToken() to trigger this
  // interceptor recursively while trying to log in.
  if (req.url.includes('/api/auth/login')) {
    return next(req);
  }

  const auth = inject(AuthService);

  return auth.getAccessToken().pipe(
    switchMap((token) =>
      next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }))
    )
  );
};
