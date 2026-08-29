import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';

export interface AuthorStat {
  author: string;
  quoteCount: number;
}

@Injectable({ providedIn: 'root' })
export class AuthorStatsService {
  private readonly http = inject(HttpClient);

  getAuthorStats(): Observable<AuthorStat[]> {
    return this.http.get<AuthorStat[]>(`${environment.functionsBaseUrl}/api/authors/stats`);
  }
}
