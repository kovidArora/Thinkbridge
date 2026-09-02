using Microsoft.EntityFrameworkCore;

namespace OutboxPatternDemo;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ConsumedMessage> ConsumedMessages => Set<ConsumedMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>().HasKey(o => o.Id);
        modelBuilder.Entity<OutboxMessage>().HasKey(m => m.Id);
        modelBuilder.Entity<ConsumedMessage>().HasKey(c => c.MessageId);
    }
}
