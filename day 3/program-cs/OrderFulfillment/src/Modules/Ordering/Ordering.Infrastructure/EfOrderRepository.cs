using Microsoft.EntityFrameworkCore;
using Ordering.Application;
using Ordering.Domain;

namespace Ordering.Infrastructure;

public class EfOrderRepository(OrderingDbContext db) : IOrderRepository
{
    // Owned collections (Lines) load automatically with their owner — no
    // explicit .Include() needed or, for a read-only IReadOnlyList exposed
    // over a private backing field, reliably expressible as one.
    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public void Add(Order order) => db.Orders.Add(order);
}

public class EfUnitOfWork(OrderingDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
