import { Component, effect, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Quote } from '../quotes/quote.model';
import { QuotesService } from '../quotes/quotes.service';
import { AuthService } from '../create-quote/auth.service';
import { CreateQuoteComponent } from '../create-quote/create-quote';
import { AuthorStatsComponent } from '../author-stats/author-stats';
import { AuthorFilterService } from '../author-stats/author-filter.service';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-quotes-list-page',
  imports: [RouterLink, CreateQuoteComponent, AuthorStatsComponent],
  templateUrl: './quotes-list-page.html',
  styleUrl: './quotes-list-page.css',
})
export class QuotesListPageComponent {
  private readonly quotesService = inject(QuotesService);
  protected readonly auth = inject(AuthService);
  protected readonly authorFilter = inject(AuthorFilterService);

  protected readonly quotes = signal<Quote[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly page = signal(1);
  // Whether the last fetch returned a full page — a page shorter than that
  // means there's nothing after it, since the server always fills a page
  // before returning less than PAGE_SIZE.
  protected readonly hasNextPage = signal(false);

  constructor() {
    // Selecting a different author (or clearing the filter) always starts
    // back at page 1 — the previous page number wouldn't mean anything
    // against a differently-scoped result set.
    effect(() => {
      this.authorFilter.selectedAuthor();
      this.page.set(1);
    });

    effect(() => {
      this.loadQuotes(this.page(), this.authorFilter.selectedAuthor());
    });
  }

  protected logout(): void {
    this.auth.logout();
  }

  protected nextPage(): void {
    if (this.hasNextPage()) {
      this.page.update((p) => p + 1);
    }
  }

  protected previousPage(): void {
    if (this.page() > 1) {
      this.page.update((p) => p - 1);
    }
  }

  // Called after creating a quote — reload the current page/filter as-is
  // rather than jumping back to page 1, so a create doesn't lose the
  // caller's place in the list.
  protected reload(): void {
    this.loadQuotes(this.page(), this.authorFilter.selectedAuthor());
  }

  private loadQuotes(page: number, author: string | null): void {
    this.loading.set(true);
    this.error.set(null);

    this.quotesService.getQuotes(page, PAGE_SIZE, author).subscribe({
      next: (quotes) => {
        this.quotes.set(quotes);
        this.hasNextPage.set(quotes.length === PAGE_SIZE);
        this.loading.set(false);
      },
      error: (err: { message?: string }) => {
        this.loading.set(false);
        this.error.set(err.message ?? 'Failed to load quotes.');
      },
    });
  }
}
