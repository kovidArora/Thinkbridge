import { Component, signal } from '@angular/core';
import { AuthorStatsComponent } from './author-stats/author-stats';
import { QuotesListComponent } from './quotes/quotes-list';
import { CreateQuoteComponent } from './create-quote/create-quote';
import { CreateQuoteSignalFormsComponent } from './create-quote-signal-forms/create-quote-signal-forms';

@Component({
  imports: [AuthorStatsComponent, QuotesListComponent, CreateQuoteComponent, CreateQuoteSignalFormsComponent],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  protected readonly title = signal('quotes-authors-web');
}
