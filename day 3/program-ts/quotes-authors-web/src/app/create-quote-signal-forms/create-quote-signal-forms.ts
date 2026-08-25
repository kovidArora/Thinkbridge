import { Component, inject, signal, ElementRef, viewChild } from '@angular/core';
import { form, required, maxLength, submit, FormField, FormRoot } from '@angular/forms/signals';
import { firstValueFrom } from 'rxjs';
import { CreateQuoteService } from '../create-quote/create-quote.service';

@Component({
  selector: 'app-create-quote-signal-forms',
  imports: [FormField, FormRoot],
  templateUrl: './create-quote-signal-forms.html',
  styleUrl: './create-quote-signal-forms.css',
})
export class CreateQuoteSignalFormsComponent {
  private readonly createQuoteService = inject(CreateQuoteService);

  protected readonly serverError = signal<string | null>(null);
  protected readonly success = signal(false);

  private readonly model = signal({ author: '', text: '' });

  protected readonly quoteForm = form(this.model, (path) => {
    required(path.author, { message: 'Author is required.' });
    maxLength(path.author, 200, { message: 'Author must be 200 characters or fewer.' });
    required(path.text, { message: 'Quote text is required.' });
    maxLength(path.text, 1000, { message: 'Quote text must be 1000 characters or fewer.' });
  });

  private readonly authorInput = viewChild<ElementRef<HTMLInputElement>>('authorInput');
  private readonly textInput = viewChild<ElementRef<HTMLTextAreaElement>>('textInput');

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.success.set(false);
    this.serverError.set(null);

    this.quoteForm().markAsTouched();

    if (!this.quoteForm().valid()) {
      this.focusFirstInvalidControl();
      return;
    }

    await submit(this.quoteForm, async () => {
      try {
        await firstValueFrom(this.createQuoteService.createQuote(this.quoteForm().value()));
        this.success.set(true);
        this.model.set({ author: '', text: '' });
      } catch {
        this.serverError.set('Failed to create quote.');
      }
      return undefined;
    });
  }

  private focusFirstInvalidControl(): void {
    if (this.quoteForm.author().invalid()) {
      this.authorInput()?.nativeElement.focus();
    } else if (this.quoteForm.text().invalid()) {
      this.textInput()?.nativeElement.focus();
    }
  }
}
