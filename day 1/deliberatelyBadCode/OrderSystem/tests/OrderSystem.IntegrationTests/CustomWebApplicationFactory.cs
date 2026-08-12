using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderSystem.Domain.Entities;
using OrderSystem.Infrastructure.Persistence;

namespace OrderSystem.IntegrationTests;

/// <summary>
/// Boots the real API pipeline (DI, controllers, exception handler,
/// middleware) against a fresh, isolated in-memory database per test run,
/// seeded with just enough data to exercise the order-creation flow
/// end-to-end.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
               options.UseInMemoryDatabase("OrderSystemIntegrationTests"));

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            db.Customers.Add(new Customer
            {
                Id = 1,
                Name = "Ada Lovelace",
                Email = "ada@example.com",
                IsActive = true,
                IsVip = false
            });

            db.Products.AddRange(
                new Product { Id = 1, Name = "Keyboard", Price = 50m, Stock = 100, IsActive = true },
                new Product { Id = 2, Name = "Mouse", Price = 25m, Stock = 100, IsActive = true },
                new Product { Id = 3, Name = "Monitor", Price = 200m, Stock = 100, IsActive = true });

            db.SaveChanges();
        });
    }
}
