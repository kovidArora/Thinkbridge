import { Component, inject } from '@angular/core';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';

@Component({
  selector: 'app-home',
  imports: [RouterLink],
  templateUrl: './home.html',
  styleUrl: './home.css',
})
export class HomeComponent {
  private readonly route = inject(ActivatedRoute);

  // Set by authGuard when it redirects an unauthenticated /quotes visit back
  // here — see auth.guard.ts.
  protected readonly unauthorized = toSignal(
    this.route.queryParamMap.pipe(map((params) => params.get('reason') === 'unauthorized')),
    { initialValue: false },
  );
}
