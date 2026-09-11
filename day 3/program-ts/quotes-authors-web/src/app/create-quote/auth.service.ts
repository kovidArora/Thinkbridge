import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, of, shareReplay, map, tap } from 'rxjs';
import { environment } from '../../environments/environment';

interface LoginResponse {
  access_token: string;
  refresh_token: string;
  expires_in: number;
}

// Session survives a page reload only because the access token itself is
// stashed here — without this, AuthService (and its isAuthenticated signal)
// would reset to "logged out" on every reload, since a reload throws away
// all in-memory state and starts a fresh Angular app instance.
const ACCESS_TOKEN_KEY = 'quotes-access-token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private token$: Observable<string> | null = null;

  readonly isAuthenticated = signal(false);

  constructor() {
    const storedToken = this.readStoredToken();
    if (storedToken) {
      // Not re-validated against the backend here — an expired or
      // otherwise invalid token just means the next authenticated request
      // gets a real 401 from the server, same as any other auth failure.
      this.token$ = of(storedToken).pipe(shareReplay(1));
      this.isAuthenticated.set(true);
    }
  }

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
    this.clearStoredToken();
  }

  private requestToken(url: string, body: { email: string; password: string }): Observable<string> {
    return this.http.post<LoginResponse>(url, body).pipe(
      map((response) => response.access_token),
      tap((token) => {
        this.isAuthenticated.set(true);
        this.storeToken(token);
      }),
      shareReplay(1)
    );
  }

  private readStoredToken(): string | null {
    try {
      return localStorage.getItem(ACCESS_TOKEN_KEY);
    } catch {
      // Storage can throw in some contexts (private browsing, disabled
      // storage) — treat that the same as "nobody logged in yet".
      return null;
    }
  }

  private storeToken(token: string): void {
    try {
      localStorage.setItem(ACCESS_TOKEN_KEY, token);
    } catch {
      // Session still works for this tab via the in-memory token$; it just
      // won't survive a reload.
    }
  }

  private clearStoredToken(): void {
    try {
      localStorage.removeItem(ACCESS_TOKEN_KEY);
    } catch {
      // Nothing to do if storage isn't available at all.
    }
  }
}
