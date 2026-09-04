using Microsoft.EntityFrameworkCore;
using Ordering.Domain;
using SharedKernel;

namespace Ordering.Infrastructure;

/// Ordering's own schema — physically the same SQLite/SQL Server instance as
/// the other modules for now (one process, one deployable), but the schema
/// boundary is real: nothing outside this DbContext ever queries these
/// tables directly. That's what makes this a modular monolith rather than
/// just "one big shared database" — splitting it into separate physical
/// databases later, if a module ever needs to scale independently, is a
/// deployment change, not a redesign.
public class OrderingDbContext(DbContextOptions<OrderingDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("ordering");

        modelBuilder.Entity<Order>(b =>
        {
            b.HasKey(o => o.Id);
            b.OwnsMany(o => o.Lines, lb =>
            {
                lb.WithOwner().HasForeignKey("OrderId");
                lb.HasKey(l => l.Id);
            });
        });

        modelBuilder.Entity<OutboxMessage>(b => b.HasKey(m => m.Id));
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Same guarantee as OutboxPattern-Demo: every pending event on every
        // tracked aggregate becomes an outbox row in this SAME SaveChanges
        // call, so the domain write and "this needs publishing" can never
        // diverge even if the process dies immediately after this commits.
        var aggregatesWithEvents = ChangeTracker.Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .Where(a => a.PendingEvents.Count > 0)
            .ToList();

        foreach (var aggregate in aggregatesWithEvents)
        {
            foreach (var @event in aggregate.PendingEvents)
            {
                OutboxMessages.Add(OutboxMessage.From(@event));
            }
            aggregate.ClearPendingEvents();
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}
