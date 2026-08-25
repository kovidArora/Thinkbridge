import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay, map } from 'rxjs';

interface LoginResponse {
  access_token: string;
  refresh_token: string;
  expires_in: number;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private token$: Observable<string> | null = null;

  getAccessToken(): Observable<string> {
    this.token$ ??= this.http
      .post<LoginResponse>('/api/auth/login', {
        email: 'test@example.com',
        password: 'Password123!',
      })
      .pipe(
        map((response) => response.access_token),
        shareReplay(1)
      );

    return this.token$;
  }
}
