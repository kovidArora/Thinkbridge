# OrderSystem

Layered refactor of the original single-file `OrderController`. This note explains the repo layout, what changed and why, and exact commands to build, test, and run it from scratch.

> **Build status honesty check:** this solution was written and reviewed by hand in a sandbox that cannot reach `api.nuget.org`, so I could not run `dotnet restore`/`build`/`test` here to prove it compiles. Every file was checked by eye (namespaces, using statements, brace balance, method signatures line up across interfaces/implementations), but treat the first `dotnet build` on your machine as the real compile check, not a formality.

## Layout

```
OrderSystem.sln
src/
  OrderSystem.Domain          # entities + enums, no dependencies on anything
  OrderSystem.Application     # DTOs, interfaces, business logic (OrderService, pricing, review policy) — no EF, no ASP.NET
  OrderSystem.Infrastructure  # EF Core DbContext, repositories, fake payment gateway, notification service
  OrderSystem.Api             # thin controller, DI wiring (Program.cs), global exception handler
tests/
  OrderSystem.UnitTests        # 3 tests, no DB, no HTTP
  OrderSystem.IntegrationTests # 1 test, real pipeline via WebApplicationFactory
```

Dependency direction is one-way: `Api -> Infrastructure -> Application -> Domain` (Api also depends on Application directly for its interfaces). Domain has zero dependencies. Nothing in Application references EF Core or ASP.NET — that's what makes `OrderService` and `OrderPricingService` unit-testable without a database or an HTTP pipeline.

## What changed vs. the original, and why

**Controller → Service → Repository, via DI**
`OrdersController` now only translates HTTP ↔ `IOrderService` calls and maps a typed `Result<OrderResponse>` to a status code. All business logic lives in `OrderService` (Application layer), which depends only on repository *interfaces* (`ICustomerRepository`, `IProductRepository`, ...), not EF Core directly. The EF Core implementations live in Infrastructure and are wired up in `Program.cs`. This is what makes it possible to unit test `OrderService`/`OrderPricingService` with no database at all, and to swap the EF/InMemory persistence for SQL Server/Postgres later by changing only `Program.cs`.

**Empty catches replaced**
Every silent `catch { /* comment */ }` from the original is gone. What replaced each one:
- Payment gateway call: narrow `catch (PaymentGatewayException ex)` — logs and **rethrows**, because a gateway fault is an infrastructure failure, not a business outcome, and should surface as a 500 via the global exception handler, not a fake success.
- Coupon usage tracking, notes trimming, reference generation: try/catch **removed entirely**. These aren't exception-prone (trimming a string, incrementing an int, formatting a GUID), and the coupon increment now saves atomically with the rest of the order in one `SaveChangesAsync` call, so there's nothing to guard against.
- Notification (confirmation email / review alert): narrow `catch (NotificationException ex)` — logs and **does not rethrow**. This is the one deliberate deviation from "log and rethrow": by this point the order is already committed. Rethrowing here would convert an already-successful order creation into a 500 response to the caller, which is worse than the original bug. This is called out explicitly in a code comment in `OrderService.SendBestEffortNotificationsAsync` so it doesn't read as a leftover swallow-and-ignore.
- The top-level `catch (NullReferenceException)` / `catch (Exception ex)` around the whole action is gone. Unhandled exceptions now flow to `GlobalExceptionHandler` (`IExceptionHandler`, ASP.NET Core 8), which logs the full exception and returns a proper `500` `ProblemDetails` body — visible in logs and in the response, not disguised as `{ success: false }`.

**Async end-to-end with cancellation**
Every repository call and `SaveChangesAsync` is awaited (`FirstOrDefaultAsync`, `ToListAsync`, etc.) and takes a `CancellationToken` threaded from the controller action all the way down. The original's synchronous `_db.SaveChanges()` inside an `async Task<object>` action — a thread-pool-blocking bug — no longer exists.

**Typed return shape**
`Result<OrderResponse>` (Application layer) replaces the anonymous `new { success = false, message = "..." }` objects. It carries an `ErrorType` (`Validation`, `NotFound`, `Conflict`, `PaymentDeclined`), which `OrdersController` maps to the correct HTTP status (`400`, `404`, `409`, `402`) instead of returning `200` for every outcome including failures. `OrderResponse`/`OrderItemResponse` are `record` types instead of anonymous objects, so the shape is documented and checkable at compile time (and shows up correctly in Swagger).

**The off-by-one bug**
The original's
```csharp
var lastItem = request.Items[request.Items.Count]; // Count is one past the last index
```
threw `IndexOutOfRangeException` on every order with 3+ items, silently masked by the catch-all as a generic 500. That block served no clear purpose beyond appending a product name to `Notes`, so rather than patch the index, it's removed. In its place, the "large-quantity" review rule (which in the original only ever checked `request.Items[0]`, the first item) is replaced with `OrderReviewPolicy.RequiresManualReview`, which checks **every** line — a real fix, not just a crash fix. See the integration test and the `OrderReviewPolicyTests` unit test below, which target these two things specifically.

**Other fixes folded in along the way** (see also the earlier `order-controller-review.md`): non-atomic order-reference generation (`_db.Orders.Count() + 1`) replaced with a collision-free `OrderReferenceGenerator`; discount/tax calculation, which the original computed twice, now computed once in `OrderPricingService`; stock decrement and order persistence now happen in one `SaveChangesAsync` call instead of separately.

## Tests

**3 unit tests** (`tests/OrderSystem.UnitTests`, no DB, no HTTP):
1. `OrderPricingServiceTests.Calculate_AppliesVolumeDiscount_VipDiscount_AndTax` — volume + VIP discount stacking, tax, and free-shipping threshold math.
2. `OrderPricingServiceTests.Calculate_CapsCombinedDiscount_AtSubtotal` — a coupon larger than the subtotal can't push the discount above 100% or the tax negative.
3. `OrderReviewPolicyTests.RequiresManualReview_FlagsAnyOversizedLine_NotJustTheFirst` — **this is the one that fails against the original's logic and passes against the fix.** The original only ever checked `request.Items[0]`; this test puts the oversized quantity on the *second* line and asserts review is still triggered.

**1 integration test** (`tests/OrderSystem.IntegrationTests`, real ASP.NET Core pipeline via `WebApplicationFactory<Program>`, isolated in-memory DB per run):
- `OrdersControllerTests.CreateOrder_WithThreeOrMoreItems_ReturnsCreated` — posts an order with 3 items. **Against the original controller this request throws `IndexOutOfRangeException` and comes back as a masked `500`. Against the refactor it must come back `201 Created`.** This is the most direct regression test for the crash bug.

## Running it from scratch

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and network access to `api.nuget.org` (the sandbox this was built in doesn't have that, so this is genuinely untested — start here).

```bash
# 1. Get the code (adjust the path if you unzip elsewhere)
cd OrderSystem

# 2. Restore all projects
dotnet restore

# 3. Build the whole solution
dotnet build

# 4. Run the tests
dotnet test
# You should see 4 tests total: 3 in OrderSystem.UnitTests, 1 in OrderSystem.IntegrationTests, all passing.

# 5. Run the API
dotnet run --project src/OrderSystem.Api
# In Development, this seeds an in-memory DB with sample customers/products/coupons on startup
# (see src/OrderSystem.Infrastructure/Seed/DbSeeder.cs) and opens Swagger UI at
# https://localhost:<port>/swagger (the exact port is printed in the console on startup).
```

### Try it with curl

Once `dotnet run` is up (replace `<port>` with what the console printed):

```bash
curl -X POST http://localhost:<port>/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": 1,
    "paymentMethod": "Cod",
    "items": [
      { "productId": 1, "quantity": 1 },
      { "productId": 2, "quantity": 2 }
    ]
  }'
```

A successful order returns `201 Created` with a `Location` header pointing at `GET /api/orders/{id}`. Try `customerId: 999` (not found → `404`), or three items where one has `quantity: 500` (→ order created with `status: "RequiresReview"`) to see the fixed review rule in action.

### If you'd rather verify just the bug fix without a full `dotnet run`

```bash
dotnet test tests/OrderSystem.IntegrationTests --filter CreateOrder_WithThreeOrMoreItems_ReturnsCreated
```

### Swapping in a real database later

Change one block in `src/OrderSystem.Api/Program.cs`:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("OrdersDb"));
```

to, e.g.:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
```

and add the `Npgsql.EntityFrameworkCore.PostgreSQL` package reference to `OrderSystem.Infrastructure.csproj`. Nothing else in the solution needs to change — that's the point of the repository/unit-of-work interfaces living in Application.

## Known gaps / things I did not try to fix here

These are called out rather than silently left, since some are legitimately out of scope for "refactor the layering":
- **Payment gateway is fake** (`FakePaymentGateway` always approves). A real integration should also never receive a raw card number — the DTO field is kept only to preserve the original's shape; swap it for a tokenized payment method from a real processor before this goes anywhere near production.
- **No authentication/authorization** on the endpoint — anyone can create an order for any `customerId`. Add `[Authorize]` plus an ownership check once there's an auth scheme in place.
- **Stock decrement concurrency**: two simultaneous requests against the *same* `DbContext`-per-request will still race at the database level under real concurrent load; add an EF Core concurrency token (`[ConcurrencyCheck]`/`RowVersion`) on `Product.Stock` if this goes on a real DB with concurrent traffic.
