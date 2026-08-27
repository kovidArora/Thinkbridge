import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { switchMap } from 'rxjs';
import { AuthService } from '../../create-quote/auth.service';

const PUBLIC_AUTH_ROUTES = ['/api/auth/login', '/api/auth/register'];

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  // Never attach a token to login/register themselves — those requests ARE how a token gets
  // obtained. Doing this unconditionally would make the current token depend on the very
  // request it's supposed to authorize, deadlocking.
  if (PUBLIC_AUTH_ROUTES.some((route) => req.url.includes(route))) {
    return next(req);
  }

  const auth = inject(AuthService);
  const token$ = auth.getCurrentToken();

  // Nobody has logged in / signed up yet — forward the request as-is. Public endpoints
  // (GET /api/quotes, GET /api/authors/stats) work fine without a token; anything that
  // genuinely requires one will correctly get a real 401 from the backend.
  if (!token$) {
    return next(req);
  }

  return token$.pipe(
    switchMap((token) => next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })))
  );
};
