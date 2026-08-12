using OrderSystem.Domain.Entities;
using OrderSystem.Domain.Enums;
using OrderSystem.Infrastructure.Persistence;

namespace OrderSystem.Infrastructure.Seed;

/// <summary>
/// Dev-only convenience seeding so `dotnet run` gives you data to hit
/// immediately with curl/Swagger. Not used in production (Program.cs only
/// calls this when the environment is Development).
/// </summary>
public static class DbSeeder
{
    public static void Seed(AppDbContext context)
    {
        if (context.Customers.Any())
        {
            return;
        }

        context.Customers.AddRange(
            new Customer { Id = 1, Name = "Ada Lovelace", Email = "ada@example.com", IsActive = true, IsVip = false },
            new Customer { Id = 2, Name = "Grace Hopper", Email = "grace@example.com", IsActive = true, IsVip = true });

        context.Products.AddRange(
            new Product { Id = 1, Name = "Mechanical Keyboard", Price = 89.99m, Stock = 50, IsActive = true },
            new Product { Id = 2, Name = "Wireless Mouse", Price = 29.99m, Stock = 100, IsActive = true },
            new Product { Id = 3, Name = "4K Monitor", Price = 349.00m, Stock = 20, IsActive = true },
            new Product { Id = 4, Name = "Discontinued Webcam", Price = 49.00m, Stock = 0, IsActive = false });

        context.Coupons.Add(new Coupon
        {
            Id = 1,
            Code = "WELCOME10",
            IsActive = true,
            Type = CouponType.Percentage,
            Value = 10m,
            MinimumOrderAmount = 50m,
            ExpiresAt = DateTime.UtcNow.AddYears(1)
        });

        context.Addresses.Add(new Address
        {
            Id = 1,
            CustomerId = 1,
            Line1 = "1 Analytical Engine Way",
            City = "London",
            PostalCode = "SW1A 1AA"
        });

        context.SaveChanges();
    }
}
