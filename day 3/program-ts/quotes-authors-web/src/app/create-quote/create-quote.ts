import { Component, ElementRef, inject, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { CreateQuoteService } from './create-quote.service';
import { AppHttpError } from '../core/http/app-http-error';

@Component({
  selector: 'app-create-quote',
  imports: [ReactiveFormsModule],
  templateUrl: './create-quote.html',
  styleUrl: './create-quote.css',
})
export class CreateQuoteComponent {
  private readonly fb = inject(FormBuilder);
  private readonly createQuoteService = inject(CreateQuoteService);
  private readonly elementRef: ElementRef<HTMLElement> = inject(ElementRef);

  protected readonly submitting = signal(false);
  protected readonly serverError = signal<string | null>(null);
  protected readonly success = signal(false);

  readonly created = output<void>();

  protected readonly form = this.fb.nonNullable.group({
    author: ['', [Validators.required, Validators.maxLength(200)]],
    text: ['', [Validators.required, Validators.maxLength(1000)]],
  });

  protected get authorControl() {
    return this.form.controls.author;
  }

  protected get textControl() {
    return this.form.controls.text;
  }

  protected onSubmit(): void {
    this.success.set(false);
    this.serverError.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.focusFirstInvalidControl();
      return;
    }

    this.submitting.set(true);

    this.createQuoteService.createQuote(this.form.getRawValue()).subscribe({
      next: () => {
        this.submitting.set(false);
        this.success.set(true);
        this.form.reset();
        this.created.emit();
      },
      error: (err: AppHttpError) => {
        this.submitting.set(false);
        this.serverError.set(err.message ?? 'Failed to create quote.');
      },
    });
  }

  private focusFirstInvalidControl(): void {
    const firstInvalid = this.elementRef.nativeElement.querySelector<HTMLElement>(
      '.ng-invalid[formControlName]'
    );
    firstInvalid?.focus();
  }
}
