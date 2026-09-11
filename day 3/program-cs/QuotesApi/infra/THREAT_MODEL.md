# Threat model — quotes-infra-stacks (STRIDE-lite)

Scope: the capstone infra stack deployed via `azd` + Deployment Stacks
(`infra/main.bicep` and modules) — the API Container App, Azure SQL,
Service Bus (design-only, not deployed for real), Key Vault, and the
network/private-endpoint layer. Not a formal enterprise threat model —
one realistic pass per component, per STRIDE category, noting what's
already mitigated and what's a known gap.

## API (Container App)

| Threat | Realistic scenario | Mitigation today |
|---|---|---|
| Spoofing | A caller presents a forged or stolen JWT to impersonate a user. | Entra ID validates tokens against the tenant's public JWKS (no shared secret to leak); the legacy "Internal" JWT scheme's signing key lives in Key Vault, never in plaintext app settings. |
| Tampering | A request body is modified in transit, or an attacker crafts an oversized/malformed payload to degrade the service. | TLS via Container Apps ingress. **Gap, addressed this task**: no explicit request-size cap or page-size ceiling existed before — see OpenAPI hardening below. |
| Repudiation | A user denies having created/deleted a quote. | `CreatedByUserId` is recorded on every quote; Container App logs flow to Log Analytics / Application Insights (see OrderFulfillment's OpenTelemetry wiring for the pattern this stack would extend to). |
| Information disclosure | Verbose error responses leak stack traces or internal details. | **Gap** — not yet audited here; flagged for the ZAP baseline pass to catch concretely rather than guessing. |
| Denial of service | A flood of requests, or a single pathological request (unbounded `size` query param), exhausts the backend. | Container Apps autoscale (`minReplicas`/`maxReplicas`) absorbs volume; **gap fixed this task** — `size` had no upper bound before, letting one caller request an unbounded result set. |
| Elevation of privilege | A caller without the right claim deletes or edits a quote they don't own. | `RequireAuthorization("can-edit-quotes")` / `"must-own-quote")` policies gate the relevant endpoints already. |

## Worker path (OutboxDispatcherBackgroundService, OrderFulfillment)

Not part of this stack's real deployment, but the same reasoning applies
to any future worker added here: a compromised or buggy dispatcher could
replay or fabricate events. Today's design already limits blast radius —
the dispatcher only reads its own module's outbox table and only invokes
already-registered event types (`EventTypesByName`), so an unrecognized
event type is logged and skipped, not executed.

## Data tier (Azure SQL)

| Threat | Realistic scenario | Mitigation |
|---|---|---|
| Spoofing | A non-app client with a leaked connection string or password connects directly to the database. | AAD-only application access via the API's managed identity — no password ever appears in an app setting. Admin password still exists for break-glass, deploy-time-only, never stored in a params file. |
| Tampering | Data modified outside the app's own validation. | AAD admin scoped to the API's own identity; the SQL login is break-glass only. |
| Information disclosure | The database is queried directly from the public internet, bypassing the API entirely. | **This task's main fix**: `publicNetworkAccess: 'Disabled'` on the SQL Server, reachable only via the private endpoint inside `vnet-quotes-<env>`. Previously reachable from any Azure service (`AllowAzureServices` firewall rule, effectively any Azure tenant's outbound traffic) — that rule is now removed entirely rather than narrowed, since there's no legitimate public caller left. |
| Denial of service | Connection exhaustion or a runaway query against the serverless tier. | `autoPauseDelayMinutes` + the free-limit tier's `AutoPause` exhaustion behavior means a runaway workload pauses rather than runs up real cost — a cost-safety control that doubles as a crude availability backstop. |
| Elevation of privilege | The app's identity is granted more than it needs. | **Known gap, pre-existing and unchanged by this task**: the API's identity is registered as full SQL AAD *admin*, not scoped to `db_datareader`/`db_datawriter` roles on just its own database — broader than necessary. Narrowing this needs a `deploymentScript`/T-SQL step Bicep can't express natively; documented here as follow-up, not fixed in this pass. |

**Known limitation**: the Container Apps Environment this stack's API
runs in (`cae-33kewg57w25su`) was created without VNet integration and
can't be retrofitted — Azure doesn't support adding a VNet to an existing
Environment, and this subscription caps Container Apps Environments at
one, so a second, VNet-integrated Environment isn't an option without
risking the real live `quotes-api` deployment that shares it. Net effect:
the *API itself* still can't reach this now-private SQL server privately
— but since the real app is hardcoded to SQLite and never actually
queries this SQL server, that's moot for this stack's real behavior. The
private endpoint was verified reachable from *inside* the VNet (a
short-lived Container Instance in a delegated subnet) and confirmed
*unreachable* from the public internet — see verification section below.

## Secrets (Key Vault)

| Threat | Mitigation |
|---|---|
| Spoofing / Elevation of privilege | RBAC-only (`enableRbacAuthorization: true`), not access policies; only the API's user-assigned pull identity holds `Key Vault Secrets User`, read-only. |
| Information disclosure | Confirmed (prior task) that no app setting ever holds a plaintext secret value — only `secretRef` pointers resolved at container-start via managed identity. |

## Message queue (Service Bus) — design-only, not deployed for real

Not a live attack surface today (`deployServiceBus` stays `false` — no
free tier for Standard-tier topics). The module grants the API's identity
Service Bus **Data Owner** — broader than Send+Listen would need;
documented as the same category of over-broad-grant gap as the SQL AAD
admin above, for the same reason (narrower RBAC roles exist but weren't
wired up in this exercise).

## Private endpoint — verified, not just deployed

Deployed for real (`az deployment` via `azd`, Deployment Stacks), then
verified from both directions rather than trusted on paper:

- **From outside the VNet** (this machine): `sqlcmd` login attempt against
  the server's public hostname was explicitly rejected:
  > `mssql: login error: ... Connection was denied because Deny Public Network Access is set to Yes.`
- **From inside the VNet**: a short-lived Azure Container Instance
  (`mcr.microsoft.com/azure-cli`, deleted immediately after) in the
  delegated `snet-connectivity-check` subnet resolved the same hostname
  to `10.20.1.4` (a private IP in the VNet's own address space) and
  successfully opened a TCP connection to port 1433.

Same hostname, two different outcomes depending on which network it's
resolved and reached from — that's the private endpoint working as
intended, not an assumption.

## OWASP ZAP baseline — API hardening pass

Ran against the local API (`GET /api/quotes?page=1&size=5`, the real
JSON surface — the bare root path returns 404 and gives the passive
scanner nothing to actually inspect). Full reports in
`infra/security/zap-baseline-report.html` / `.json`.

**Before fixes**: 3 new warnings, 0 failures —
`X-Content-Type-Options Header Missing`,
`Cross-Origin-Resource-Policy Header Missing`,
`Storable and Cacheable Content` on the real API response.

**Fixed directly** (global response-header middleware in `Program.cs`):
`X-Content-Type-Options: nosniff`, `Cross-Origin-Resource-Policy:
same-origin`, `Cache-Control: no-store` — appropriate for a JSON API with
no static assets and some auth-gated responses that should never be
cached anywhere.

**After fixes**: 0 failures, 1 low-severity warning remaining — default
ASP.NET Core 404 pages (root path, a probed `sitemap.xml` that doesn't
exist) showing up as non-storable content. Not a real finding against
this API's actual surface; left as-is.

**Known gap this pass didn't cover**: the API exposes no OpenAPI/Swagger
document at all (`/swagger/v1/swagger.json` and `/openapi/v1.json` both
404). ZAP's baseline scan works passively/via spider regardless, but a
published OpenAPI spec would let it (and any API-aware tooling) scan far
more thoroughly. Worth adding as a follow-up, separate from this task's
scope.

## Summary of gaps closed this task vs. carried forward

**Closed:**
- SQL Server public network access — now fully disabled, private endpoint only, verified from both sides.
- Unbounded `size` query parameter on `GET /api/quotes` and `GET /api/quotes/with-authors` — now capped at 100, and `with-authors` had no validation at all before.
- `GET /api/quotes/with-authors` (exposes author email addresses) — now requires authentication.
- `/api/debug/*` endpoints — now require authentication (previously callable by anyone, including a free cache-eviction DoS lever).
- `/api/collections*` write endpoints — now require authentication (a previously-known, documented gap).
- API versioning added (`/api/v1/*`, rewriting onto the existing routes with zero duplication or behavior change).
- Three real ZAP baseline findings — closed via response headers.

**Carried forward, documented, not fixed here:**
- API's SQL identity is full AAD admin, not scoped `db_datareader`/`db_datawriter`.
- Service Bus identity grant is Data Owner, not narrower Send+Listen roles.
- No published OpenAPI/Swagger document.
- API itself has no private network path to SQL (architectural constraint, not a code gap — see Known limitation above).
