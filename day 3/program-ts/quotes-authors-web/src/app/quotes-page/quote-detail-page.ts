import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { map, switchMap, catchError, of, tap } from 'rxjs';
import { Quote } from '../quotes/quote.model';
import { QuotesService } from '../quotes/quotes.service';

@Component({
  selector: 'app-quote-detail-page',
  imports: [RouterLink],
  templateUrl: './quote-detail-page.html',
  styleUrl: './quote-detail-page.css',
})
export class QuoteDetailPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly quotesService = inject(QuotesService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  protected readonly quote = toSignal(
    this.route.paramMap.pipe(
      map((params) => Number(params.get('id'))),
      switchMap((id) => {
        this.loading.set(true);
        this.error.set(null);

        return this.quotesService.getQuoteById(id).pipe(
          tap(() => this.loading.set(false)),
          catchError((err: { message?: string }) => {
            this.loading.set(false);
            this.error.set(err.message ?? 'Failed to load quote.');
            return of(null);
          })
        );
      })
    ),
    { initialValue: null as Quote | null }
  );
}
