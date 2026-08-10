using Microsoft.EntityFrameworkCore;
using OrderSystem.Api.ErrorHandling;
using OrderSystem.Application.Interfaces;
using OrderSystem.Application.Pricing;
using OrderSystem.Application.Services;
using OrderSystem.Infrastructure.Notifications;
using OrderSystem.Infrastructure.Payments;
using OrderSystem.Infrastructure.Persistence;
using OrderSystem.Infrastructure.Persistence.Repositories;
using OrderSystem.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Swap this for options.UseSqlServer(...) / UseNpgsql(...) against a real
// database in non-Development environments. InMemory is used here so the
// sample runs with zero external setup.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("OrdersDb"));

builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICouponRepository, CouponRepository>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

builder.Services.AddSingleton<IOrderPricingService, OrderPricingService>();
builder.Services.AddSingleton<IOrderReferenceGenerator, OrderReferenceGenerator>();
builder.Services.AddScoped<IPaymentGateway, FakePaymentGateway>();
builder.Services.AddScoped<IOrderNotificationService, LoggingNotificationService>();
builder.Services.AddScoped<IOrderService, OrderService>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbSeeder.Seed(db);
}

app.MapControllers();

app.Run();

// Exposed so WebApplicationFactory<Program> can bootstrap the app in
// integration tests.
public partial class Program { }
