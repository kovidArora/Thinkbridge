import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Quote } from './quote.model';

@Injectable({ providedIn: 'root' })
export class QuotesService {
  private readonly http = inject(HttpClient);

  getQuotes(page: number, size: number): Observable<Quote[]> {
    return this.http.get<Quote[]>('/api/quotes', {
      params: { page, size },
    });
  }

  getQuoteById(id: number): Observable<Quote> {
    return this.http.get<Quote>(`/api/quotes/${id}`);
  }
}
