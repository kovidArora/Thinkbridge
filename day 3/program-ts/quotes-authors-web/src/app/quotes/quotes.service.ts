import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Quote } from './quote.model';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class QuotesService {
  private readonly http = inject(HttpClient);

  getQuotes(page: number, size: number): Observable<Quote[]> {
    return this.http.get<Quote[]>(`${environment.functionsBaseUrl}/api/quotes`, {
      params: { page, size },
    });
  }

  getQuoteById(id: number): Observable<Quote> {
    return this.http.get<Quote>(`${environment.functionsBaseUrl}/api/quotes/${id}`);
  }

  deleteQuote(id: number): Observable<void> {
    return this.http.delete<void>(`${environment.backendBaseUrl}/api/quotes/${id}`);
  }
}
