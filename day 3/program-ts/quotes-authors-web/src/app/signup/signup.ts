import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../create-quote/auth.service';
import { AppHttpError } from '../core/http/app-http-error';

@Component({
  selector: 'app-signup',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './signup.html',
  styleUrl: './signup.css',
})
export class SignupComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly submitting = signal(false);
  protected readonly serverError = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(8)]],
  });

  protected get emailControl() {
    return this.form.controls.email;
  }

  protected get passwordControl() {
    return this.form.controls.password;
  }

  protected onSubmit(): void {
    this.serverError.set(null);

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    const { email, password } = this.form.getRawValue();

    this.auth.register(email, password).subscribe({
      next: () => {
        this.submitting.set(false);
        this.router.navigateByUrl('/quotes');
      },
      error: (err: AppHttpError) => {
        this.submitting.set(false);
        this.serverError.set(err.message ?? 'Failed to sign up.');
      },
    });
  }
}
