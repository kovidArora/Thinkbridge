using Inventory.Application;
using Microsoft.EntityFrameworkCore;
using Ordering.Application;
using Ordering.Domain;
using Ordering.Infrastructure;
using OrderFulfillment.Api;
using SharedKernel;
using Shipping.Application;

var builder = WebApplication.CreateBuilder(args);

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
    stock.Seed("SHIRT-001", quantityOnHand: 0); // deliberately out of stock, to exercise the cancel path
}

app.MapPost("/orders", async (PlaceOrderCommand command, PlaceOrderCommandHandler handler, CancellationToken ct) =>
{
    var orderId = await handler.HandleAsync(command, ct);
    return Results.Created($"/orders/{orderId}", new { orderId });
});

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
