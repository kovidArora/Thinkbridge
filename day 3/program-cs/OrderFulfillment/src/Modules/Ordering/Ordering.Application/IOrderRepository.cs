using Ordering.Domain;

namespace Ordering.Application;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    void Add(Order order);
}

/// Deliberately separate from the repository: "add this to the change set"
/// and "commit the change set" are different concerns, and only the second
/// one is where the outbox rows for this module's pending events actually
/// get written (see OrderingDbContext.SaveChangesAsync in Infrastructure).
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
