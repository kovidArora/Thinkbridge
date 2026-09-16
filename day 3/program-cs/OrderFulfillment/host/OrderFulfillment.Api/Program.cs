using Azure.Monitor.OpenTelemetry.AspNetCore;
using Inventory.Application;
using Microsoft.EntityFrameworkCore;
using Ordering.Application;
using Ordering.Domain;
using Ordering.Infrastructure;
using OrderFulfillment.Api;
using OpenTelemetry.Trace;
using SharedKernel;
using Shipping.Application;

var builder = WebApplication.CreateBuilder(args);

// --- Observability: added explicitly and unconditionally, not left to come
// bundled from UseAzureMonitor() (which is now conditional, below) — a real
// bug this file used to have: without a connection string, UseAzureMonitor()
// never ran, which silently meant NO ASP.NET Core instrumentation either, so
// the HTTP request span (and the trace context the outbox dispatcher relies
// on to link back to it) disappeared entirely on a local run with no Azure
// credentials configured. EF Core still needs its own instrumentation added
// explicitly regardless; the outbox dispatcher's manual spans (see
// OutboxDispatcherBackgroundService) need their ActivitySource registered or
// the SDK would silently drop them too.
var tracingBuilder = builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource(OutboxDispatcherBackgroundService.ActivitySource.Name)
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation());

// Console exporter only in Development — prints every span to stdout as it
// happens, so a local run can be visually confirmed as working without
// waiting on Application Insights ingestion. Left out of every other
// environment: nobody reads a deployed Container App's console output for
// trace data, and Application Insights already has it there.
if (builder.Environment.IsDevelopment())
{
    tracingBuilder.WithTracing(tracing => tracing.AddConsoleExporter());
}

// UseAzureMonitor() throws at startup if it can't find a connection string
// anywhere — fine for a real deployment (it always has one via app
// settings/Key Vault), but it meant a bare local `dotnet run` couldn't even
// start without Azure credentials. Only wire it up when one's actually
// configured; the Development console exporter above already gives a local
// run something to look at regardless.
var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"]
    ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    tracingBuilder.UseAzureMonitor(options => options.ConnectionString = appInsightsConnectionString);
}

// --- Ordering: the only module with real EF-backed persistence in this scaffold ---
builder.Services.AddDbContext<OrderingDbContext>(options => options.UseSqlite("Data Source=orderfulfillment.db"));
builder.Services.AddScoped<IOrderRepository, EfOrderRepository>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();
builder.Services.AddScoped<PlaceOrderCommandHandler>();
builder.Services.AddScoped<ConfirmOrderOnStockReservedHandler>();
builder.Services.AddScoped<CancelOrderOnStockReservationFailedHandler>();

// --- Inventory: scaffolded with an in-memory repository (see InMemoryStockItemRepository) ---
builder.Services.AddSingleton<InMemoryStockItemRepository>();
builder.Services.AddSingleton<IStockItemRepository>(sp => sp.GetRequiredService<InMemoryStockItemRepository>());
builder.Services.AddScoped<ReserveStockOnOrderPlacedHandler>();

// --- Shipping: scaffolded, no persistence yet ---
builder.Services.AddScoped<CreateShipmentOnOrderConfirmedHandler>();

// --- Notifications: no state at all ---
builder.Services.AddScoped<Notifications.Application.NotificationHandlers>();

// --- Composition root: the dispatcher and outbox relay are the only things
// allowed to know about every module at once ---
builder.Services.AddSingleton<InProcessEventDispatcher>();
builder.Services.AddSingleton<IIntegrationEventPublisher>(sp => sp.GetRequiredService<InProcessEventDispatcher>());
// AddHostedService<T> alone only registers T as IHostedService, not
// resolvable by its own type — also needed directly for the debug endpoint
// below, so register it as itself and forward IHostedService to that instance.
builder.Services.AddSingleton<OutboxDispatcherBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<OutboxDispatcherBackgroundService>());

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderingDbContext>();
    await db.Database.EnsureCreatedAsync();

    // Seed a couple of SKUs so a demo order can actually succeed.
    var stock = scope.ServiceProvider.GetRequiredService<InMemoryStockItemRepository>();
    stock.Seed("MUG-001", quantityOnHand: 10);
    stock.Seed("SHIRT-001", quantityOnHand: 0);
     // deliberately out of stock, to exercise the cancel path
}

// customer places an order -> hand it to the handler -> 201 + the new id
app.MapPost("/orders", async (PlaceOrderCommand command, PlaceOrderCommandHandler handler, CancellationToken ct) =>
{
    var orderId = await handler.HandleAsync(command, ct);
    return Results.Created($"/orders/{orderId}", new { orderId });
});

// look up one order by id -> 404 if it's not there, 200 + a few fields if it is
app.MapGet("/orders/{id:guid}", async (Guid id, OrderingDbContext db, CancellationToken ct) =>
{
    var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct);
    return order is null
        ? Results.NotFound()
        : Results.Ok(new { order.Id, order.CustomerId, order.Status, order.Total });
});

// Test-only: force the outbox relay to run immediately instead of waiting
// for its 200ms poll, so the async flow is observable synchronously.
app.MapPost("/debug/dispatch-outbox", async (OutboxDispatcherBackgroundService dispatcher, CancellationToken ct) =>
{
    await dispatcher.DispatchPendingAsync(ct);
    return Results.NoContent();
});

app.Run();
