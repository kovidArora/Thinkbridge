import { Component, signal } from '@angular/core';
import { AuthorStatsComponent } from './author-stats/author-stats';
import { QuotesListComponent } from './quotes/quotes-list';
import { CreateQuoteComponent } from './create-quote/create-quote';

@Component({
  imports: [AuthorStatsComponent, QuotesListComponent, CreateQuoteComponent],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  protected readonly title = signal('quotes-authors-web');
}
