import { Routes } from '@angular/router';
import { authGuard } from './auth.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./home/home').then((m) => m.HomeComponent),
  },
  {
    path: 'login',
    loadComponent: () => import('./login/login').then((m) => m.LoginComponent),
  },
  {
    path: 'signup',
    loadComponent: () => import('./signup/signup').then((m) => m.SignupComponent),
  },
  {
    path: 'quotes',
    loadComponent: () =>
      import('./quotes-page/quotes-list-page').then((m) => m.QuotesListPageComponent),
    canActivate: [authGuard],
  },
  {
    path: 'quotes/:id',
    loadComponent: () =>
      import('./quotes-page/quote-detail-page').then((m) => m.QuoteDetailPageComponent),
    canActivate: [authGuard],
  },
];
