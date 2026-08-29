import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, shareReplay, map, tap } from 'rxjs';
import { environment } from '../../environments/environment';

interface LoginResponse {
  access_token: string;
  refresh_token: string;
  expires_in: number;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private token$: Observable<string> | null = null;

  readonly isAuthenticated = signal(false);

  /** The current session's token stream, or null if nobody has logged in / signed up yet. */
  getCurrentToken(): Observable<string> | null {
    return this.token$;
  }

  register(email: string, password: string): Observable<string> {
    this.token$ = this.requestToken(`${environment.backendBaseUrl}/api/auth/register`, { email, password });
    return this.token$;
  }

  login(email: string, password: string): Observable<string> {
    this.token$ = this.requestToken(`${environment.backendBaseUrl}/api/auth/login`, { email, password });
    return this.token$;
  }

  logout(): void {
    this.token$ = null;
    this.isAuthenticated.set(false);
  }

  private requestToken(url: string, body: { email: string; password: string }): Observable<string> {
    return this.http.post<LoginResponse>(url, body).pipe(
      map((response) => response.access_token),
      tap(() => this.isAuthenticated.set(true)),
      shareReplay(1)
    );
  }
}
