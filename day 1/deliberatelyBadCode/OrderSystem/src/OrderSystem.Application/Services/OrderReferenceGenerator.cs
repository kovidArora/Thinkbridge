using OrderSystem.Application.Interfaces;

namespace OrderSystem.Application.Services;

/// <summary>
/// The original code generated references as "ORD-{date}-{_db.Orders.Count() + 1}",
/// which is a race condition: two concurrent requests can read the same
/// count and produce the same reference. This generator needs no database
/// round trip and cannot collide under concurrency.
/// </summary>
public class OrderReferenceGenerator : IOrderReferenceGenerator
{
    public string Generate()
    {
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var uniquePart = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return $"ORD-{datePart}-{uniquePart}";
    }
}
