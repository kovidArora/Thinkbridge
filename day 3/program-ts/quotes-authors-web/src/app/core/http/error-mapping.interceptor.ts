import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { AppHttpError } from './app-http-error';

interface ValidationProblemDetailsBody {
  title?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

export const errorMappingInterceptor: HttpInterceptorFn = (req, next) =>
  next(req).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }

      return throwError(() => toAppHttpError(error));
    })
  );

function toAppHttpError(error: HttpErrorResponse): AppHttpError {
  const body: unknown = error.error;

  // Real shape from GET /api/quotes on page/size validation failure: ValidationProblemDetails
  // ({ title, status, errors: { page: ["Page must be greater than 0."] } }).
  if (isValidationProblemDetails(body)) {
    const fieldErrors = body.errors!;
    const message = Object.values(fieldErrors).flat().join(' ');
    return { status: error.status, message: message || body.title || 'Validation failed.', fieldErrors };
  }

  // Real shape from POST /api/quotes on a 400: a bare JSON string, NOT ProblemDetails at all
  // (e.g. "Text must be between 1 and 1000 characters."). Different endpoint, different shape.
  if (typeof body === 'string' && body.length > 0) {
    return { status: error.status, message: body };
  }

  if (error.status === 0) {
    return { status: 0, message: 'Could not reach the server. Check your connection and try again.' };
  }

  return { status: error.status, message: 'Something went wrong. Please try again.' };
}

function isValidationProblemDetails(body: unknown): body is ValidationProblemDetailsBody {
  return (
    typeof body === 'object' &&
    body !== null &&
    'errors' in body &&
    typeof (body as ValidationProblemDetailsBody).errors === 'object'
  );
}
