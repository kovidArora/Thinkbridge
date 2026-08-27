# QuotesApi + quotes-authors-web — Handover

Repo: `kovidArora/Thinkbridge`. Backend at `day 3/program-cs/QuotesApi`, frontend at `day 3/program-ts/quotes-authors-web`. Everything pushed straight to `main` (still no PR workflow — see git workflow note below).

## What this session covers

Builds directly on the Week 1 handover (backend: dual auth, observability, resilience, deployment — see `day 3/program-cs/QuotesApi-Docs` for full docs on all of that). This session's focus: EF Core internals and performance work, a full Angular frontend built against the real API, and a big pass simplifying/expanding the documentation vault.

## Backend changes

- **CQRS split** — `QuoteRepository` used to own reads, writes, and a joined read all at once. Writes moved to `Commands/CreateQuoteCommandHandler.cs`; the author-email join moved to `Queries/QuoteReadModel.cs`. `IQuoteRepository` now only owns plain reads/deletes and `GetAuthorStatsAsync`.
- **A deliberate N+1 + missing index, found and fixed** — `GET /api/authors/stats` was shipped with a real N+1 (one query per author) and no index on `Quote.Author`. Fixed with a single `GROUP BY` query plus a new composite index (`IX_Quotes_Author_IsDeleted`). Measured under load (20 concurrent, 15s): **p99 went from 3,852ms to 53ms — ~72x improvement.**
- **Dapper comparison** — `Queries/AuthorStatsDapperQuery.cs` is a hand-written-SQL equivalent of the same query, kept for comparison only (not wired into any endpoint). Real benchmark: Dapper ran ~1.38x faster than EF Core for this specific query (1.733ms vs 2.384ms avg) — see `Dapper-Vs-EF-Demo/` console app.
- **API contract consistency fix** — `POST /api/quotes`'s `400` used to be a bare JSON string; `GET /api/quotes`'s `400` was a proper `ValidationProblemDetails` object. Found via a frontend characterization test, fixed by making `POST` also return `ValidationProblemDetails`, field-keyed (`author`/`text`).
- **New endpoint: `POST /api/auth/register`** — validates email/password, checks for an existing account, hashes the password, creates the user, and logs them in immediately (same response shape as login).

## Frontend — new Angular app (`quotes-authors-web`)

Zoneless, standalone Angular 22 (`ng new --standalone --zoneless`, no NgModules, no `zone.js`). Talks to the real API only — dev-server proxy (`proxy.conf.json`) forwards `/api/*` to `localhost:5067`, no CORS changes made to the backend.

**Pages/components:**
- `author-stats/` — signals/computed/effect basics against `GET /api/authors/stats`.
- `quotes-page/` — routed list (`/quotes`) and detail (`/quotes/:id`) pages, lazy-loaded, with a `switchMap`-based guard against stale-response races on rapid navigation.
- `create-quote/` — accessible reactive-forms create-quote form against `POST /api/quotes`.
- `create-quote-signal-forms/` — the same form rebuilt on Angular's experimental Signal Forms preview API, kept in the codebase as a documented comparison (not currently rendered on the live page).
- `signup/`, `login/` — real auth forms against `POST /api/auth/register` and `POST /api/auth/login`.
- `core/http/` — three functional interceptors: auth header, retry-with-backoff (GET-only, transient failures only), and typed error mapping (`ValidationProblemDetails` → `AppHttpError`).
- `core/quotes-api-contract.spec.ts` — a characterization test hitting the **real running API** (no mocking) to pin its actual contract; written and passing before any interceptor code was built against it.
- `app.routes.ts` — lazy-loaded routes, a functional `authGuard`, route params, `withViewTransitions()`.

**Real bugs found and fixed this session (frontend):**
1. `aria-describedby` pointed at a non-existent element id on the reactive-forms create-quote input — a screen reader would announce "invalid" but never read why. Caught by checking `document.getElementById(...)` resolved to `null` in a live browser.
2. Signal Forms: imported `Field` (a TypeScript type) instead of `FormField` (the real directive) — real compile error.
3. Signal Forms: missing `[formRoot]` directive meant `(ngSubmit)` silently did nothing — the browser did an uncontrolled native form submission (full page reload, form data dumped into the URL). Caught via the network log, not the code.
4. `authInterceptor` only excluded `/api/auth/login` from getting a token attached, not the newly-added `/api/auth/register` — caused a genuine deadlock (the register request waited on itself for a token, forever). Caught by watching a real signup submission hang on "Signing up…" with zero network activity.
5. The auth interceptor used to silently auto-login as a hardcoded background test user whenever no token existed — this meant the create-quote form worked even after clicking "Log out," and there was no way to log in as an existing user at all. Removed the silent fallback, added a real login page, and gated the create-quote form behind actual auth state.
6. First draft of the lazy route config statically imported the detail page component at the top of the file — looked lazy syntactically, wasn't. Confirmed via the real build output (`quote-detail-page` missing from "Lazy chunk files"), fixed, reconfirmed present as its own chunk.

**Known rough edges, not yet addressed:**
- No session persistence — auth state is in-memory only, so a hard page reload always starts logged out (this is expected/correct given current design, not a bug, but worth knowing).
- The browser's native View Transition API throws a harmless `InvalidStateError` console error if navigations happen in very rapid succession (faster than a human would realistically click) — doesn't break navigation, just aborts the animation.
- `create-quote-signal-forms` and `Queries/AuthorStatsDapperQuery.cs` are both intentionally-kept comparison artifacts, not part of the live app/API surface.

## SQL exercises

Nine `.sql` files under `QuotesApi/sql/` — window functions/CTEs against the real `quotes.db`, set operations, and SQL Server-specific indexing/isolation-level/deadlock demos run against a throwaway Docker container. See `SQL Exercises.md` in the docs vault for the full map, and `Interview Prep - SQL.md` for theory + syntax Q&A grounded in these exact files.

## EF Core internals demo

`ChangeTracker-Demo/` — a standalone console app demonstrating identity resolution, tracked vs. `AsNoTracking()` reads (measured: ~4x slower, ~3x more allocation when tracked), generated-SQL logging, and a real client-side-evaluation bug (`ToList().Where(...)` instead of `Where(...).ToList()` — pulls the whole table into memory, still returns a correct-looking result).

## Documentation

`day 3/program-cs/QuotesApi-Docs` — an Obsidian vault, **not pushed to git** (local only, intentionally gitignored). 28 notes plus two interview-prep docs (`Interview Prep - App and EF Core.md`, `Interview Prep - SQL.md`), all rewritten this session to open with a plain-language summary before the technical detail, and to define jargon inline rather than assume it. Start at `Home.md` if picking this up fresh.

## Git workflow note (unchanged from Week 1)

Still no PR workflow — everything goes straight to `main`. Multiple pushes have happened from different sources in parallel during this session; always `git fetch` before pushing.

## Suggested next steps

- Decide whether to add session persistence (e.g. a refresh-token-backed session) so auth survives a page reload, or leave it as an intentional in-memory-only design.
- `QuotesApi.Tests` (the old, 4th test project) still isn't referenced by `QuotesApi.slnx` — clean it up or re-include it.
- Collections endpoints still have no authorization policy applied — revisit if collections become user-owned.
- Consider whether `create-quote-signal-forms` should be removed from the live app entirely (currently just unrendered but still built/shipped in the bundle if ever re-added to a route) or kept purely as an educational artifact.
