import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface AuthorStat {
  author: string;
  quoteCount: number;
}

@Injectable({ providedIn: 'root' })
export class AuthorStatsService {
  private readonly http = inject(HttpClient);

  getAuthorStats(): Observable<AuthorStat[]> {
    return this.http.get<AuthorStat[]>('/api/authors/stats');
  }
}
