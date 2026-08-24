import { Component, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, of, switchMap, tap } from 'rxjs';
import { Quote } from './quote.model';
import { QuotesService } from './quotes.service';

@Component({
  selector: 'app-quotes-list',
  imports: [],
  templateUrl: './quotes-list.html',
  styleUrl: './quotes-list.css',
})
export class QuotesListComponent {
  private readonly quotesService = inject(QuotesService);

  protected readonly quotes = signal<Quote[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly selectedId = signal<number | null>(null);
  protected readonly detailLoading = signal(false);
  protected readonly detailError = signal<string | null>(null);

  private readonly selectedQuote$ = toObservable(this.selectedId).pipe(
    switchMap((id) => {
      if (id === null) {
        this.detailLoading.set(false);
        this.detailError.set(null);
        return of(null);
      }

      this.detailLoading.set(true);
      this.detailError.set(null);

      return this.quotesService.getQuoteById(id).pipe(
        tap(() => this.detailLoading.set(false)),
        catchError(() => {
          this.detailLoading.set(false);
          this.detailError.set('Failed to load quote detail.');
          return of(null);
        })
      );
    })
  );

  protected readonly selectedQuote = toSignal(this.selectedQuote$, {
    initialValue: null,
  });

  constructor() {
    this.loadQuotes();
  }

  protected selectQuote(id: number): void {
    this.selectedId.set(id);
  }

  protected reload(): void {
    this.loadQuotes();
  }

  private loadQuotes(): void {
    this.loading.set(true);
    this.error.set(null);

    this.quotesService.getQuotes(1, 20).subscribe({
      next: (quotes) => {
        this.quotes.set(quotes);
        this.loading.set(false);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set('Failed to load quotes.');
        console.error(err);
      },
    });
  }
}
