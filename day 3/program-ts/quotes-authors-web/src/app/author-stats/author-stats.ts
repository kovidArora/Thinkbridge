import { Component, computed, effect, inject, signal } from '@angular/core';
import { AuthorStat, AuthorStatsService } from './author-stats.service';
import { AuthorFilterService } from './author-filter.service';

@Component({
  selector: 'app-author-stats',
  imports: [],
  templateUrl: './author-stats.html',
  styleUrl: './author-stats.css',
})
export class AuthorStatsComponent {
  private readonly authorStatsService = inject(AuthorStatsService);
  protected readonly authorFilter = inject(AuthorFilterService);

  protected readonly authors = signal<AuthorStat[]>([]);
  protected readonly minQuotes = signal(0);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly filteredAuthors = computed(() =>
    this.authors()
      .filter((a) => a.quoteCount >= this.minQuotes())
      .sort((a, b) => b.quoteCount - a.quoteCount)
  );

  protected readonly filteredCount = computed(() => this.filteredAuthors().length);

  constructor() {
    effect(() => {
      console.log(
        `[author-stats] threshold=${this.minQuotes()} -> ${this.filteredCount()} of ${this.authors().length} authors shown`
      );
    });

    this.loadAuthorStats();
  }

  protected setMinQuotes(value: string): void {
    const parsed = Number(value);
    this.minQuotes.set(Number.isFinite(parsed) ? parsed : 0);
  }

  protected reload(): void {
    this.loadAuthorStats();
  }

  protected toggleAuthor(author: string): void {
    if (this.authorFilter.selectedAuthor() === author) {
      this.authorFilter.clearAuthor();
    } else {
      this.authorFilter.selectAuthor(author);
    }
  }

  private loadAuthorStats(): void {
    this.loading.set(true);
    this.error.set(null);

    this.authorStatsService.getAuthorStats().subscribe({
      next: (stats) => {
        this.authors.set(stats);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set('Failed to load author stats.');
        this.loading.set(false);
        console.error(err);
      },
    });
  }
}
