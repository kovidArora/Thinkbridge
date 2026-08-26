import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { retry, timer } from 'rxjs';

const MAX_RETRIES = 3;
const BASE_DELAY_MS = 200;

export const retryInterceptor: HttpInterceptorFn = (req, next) => {
  if (req.method !== 'GET') {
    return next(req);
  }

  return next(req).pipe(
    retry({
      count: MAX_RETRIES,
      delay: (error, retryCount) => {
        const isTransient =
          error instanceof HttpErrorResponse && (error.status === 0 || error.status >= 500);

        if (!isTransient) {
          throw error;
        }

        return timer(BASE_DELAY_MS * 2 ** (retryCount - 1));
      },
    })
  );
};
