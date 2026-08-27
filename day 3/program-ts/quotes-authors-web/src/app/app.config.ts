import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideRouter, withViewTransitions } from '@angular/router';
import { authInterceptor } from './core/http/auth.interceptor';
import { errorMappingInterceptor } from './core/http/error-mapping.interceptor';
import { retryInterceptor } from './core/http/retry.interceptor';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withInterceptors([authInterceptor, errorMappingInterceptor, retryInterceptor])),
    provideRouter(routes, withViewTransitions()),
  ]
};
