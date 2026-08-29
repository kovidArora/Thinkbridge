import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Quote } from '../quotes/quote.model';
import { environment } from '../../environments/environment';

export interface CreateQuoteRequest {
  author: string;
  text: string;
}

@Injectable({ providedIn: 'root' })
export class CreateQuoteService {
  private readonly http = inject(HttpClient);

  createQuote(request: CreateQuoteRequest): Observable<Quote> {
    return this.http.post<Quote>(`${environment.backendBaseUrl}/api/quotes`, request);
  }
}
