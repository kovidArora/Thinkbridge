import { Component, signal } from '@angular/core';
import { AuthorStatsComponent } from './author-stats/author-stats';

@Component({
  imports: [AuthorStatsComponent],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  protected readonly title = signal('quotes-authors-web');
}
