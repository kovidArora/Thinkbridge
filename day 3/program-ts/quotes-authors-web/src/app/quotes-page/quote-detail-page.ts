import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { map, switchMap, catchError, of, tap } from 'rxjs';
import { Quote } from '../quotes/quote.model';
import { QuotesService } from '../quotes/quotes.service';
import { AuthService } from '../create-quote/auth.service';

@Component({
  selector: 'app-quote-detail-page',
  imports: [RouterLink],
  templateUrl: './quote-detail-page.html',
  styleUrl: './quote-detail-page.css',
})
export class QuoteDetailPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly quotesService = inject(QuotesService);
  protected readonly auth = inject(AuthService);

  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly deleting = signal(false);
  protected readonly deleteError = signal<string | null>(null);

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

  protected deleteQuote(): void {
    const q = this.quote();
    if (!q || !confirm(`Delete quote #${q.id} by ${q.author}? This can't be undone.`)) {
      return;
    }

    this.deleting.set(true);
    this.deleteError.set(null);

    this.quotesService.deleteQuote(q.id).subscribe({
      next: () => this.router.navigateByUrl('/quotes'),
      error: (err: { status?: number; message?: string }) => {
        this.deleting.set(false);
        this.deleteError.set(
          err.status === 403 || err.status === 401
            ? "You can only delete quotes you created."
            : (err.message ?? 'Failed to delete quote.')
        );
      },
    });
  }
}
