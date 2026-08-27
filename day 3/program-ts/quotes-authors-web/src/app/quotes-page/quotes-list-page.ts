import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Quote } from '../quotes/quote.model';
import { QuotesService } from '../quotes/quotes.service';
import { AuthService } from '../create-quote/auth.service';
import { CreateQuoteComponent } from '../create-quote/create-quote';
import { AuthorStatsComponent } from '../author-stats/author-stats';
import { AuthorFilterService } from '../author-stats/author-filter.service';

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

  protected readonly filteredQuotes = computed(() => {
    const selected = this.authorFilter.selectedAuthor();
    return selected ? this.quotes().filter((q) => q.author === selected) : this.quotes();
  });

  constructor() {
    this.loadQuotes();
  }

  protected logout(): void {
    this.auth.logout();
  }

  protected loadQuotes(): void {
    this.loading.set(true);
    this.error.set(null);

    this.quotesService.getQuotes(1, 20).subscribe({
      next: (quotes) => {
        this.quotes.set(quotes);
        this.loading.set(false);
      },
      error: (err: { message?: string }) => {
        this.loading.set(false);
        this.error.set(err.message ?? 'Failed to load quotes.');
      },
    });
  }
}
