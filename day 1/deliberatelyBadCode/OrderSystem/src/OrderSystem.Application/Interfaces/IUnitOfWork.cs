namespace OrderSystem.Application.Interfaces;

/// <summary>
/// Wraps a single database transaction/save. The order, its line items,
/// the product stock decrements, and the coupon usage increment are all
/// tracked in one DbContext and committed together in one
/// SaveChangesAsync call, so they succeed or fail as a unit instead of the
/// original code's separate, un-transacted operations.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
