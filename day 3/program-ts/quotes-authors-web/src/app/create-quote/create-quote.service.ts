import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, switchMap } from 'rxjs';
import { Quote } from '../quotes/quote.model';
import { AuthService } from './auth.service';

export interface CreateQuoteRequest {
  author: string;
  text: string;
}

@Injectable({ providedIn: 'root' })
export class CreateQuoteService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  createQuote(request: CreateQuoteRequest): Observable<Quote> {
    return this.auth.getAccessToken().pipe(
      switchMap((token) =>
        this.http.post<Quote>('/api/quotes', request, {
          headers: new HttpHeaders({ Authorization: `Bearer ${token}` }),
        })
      )
    );
  }
}
