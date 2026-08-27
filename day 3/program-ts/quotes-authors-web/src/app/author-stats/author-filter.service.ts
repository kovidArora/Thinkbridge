import { Injectable, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class AuthorFilterService {
  readonly selectedAuthor = signal<string | null>(null);

  selectAuthor(author: string): void {
    this.selectedAuthor.set(author);
  }

  clearAuthor(): void {
    this.selectedAuthor.set(null);
  }
}
