using Microsoft.EntityFrameworkCore;
using QuotesApi.Models;

namespace QuotesApi.Data;

// EF Core uses this class to create the database and tables

public class QuotesDbContext : DbContext
{
    public QuotesDbContext(DbContextOptions options)
        : base(options)
    {
    }

    public DbSet<Quote> Quotes { get; set; }
    public DbSet<Collection> Collections { get; set; }

   protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Collection>(entity =>
    {
        entity.HasKey(c => c.Id);

        entity.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(80);

        entity.OwnsMany(c => c.Items, item =>
        {
            item.WithOwner()
                .HasForeignKey("CollectionId");

            item.HasKey("CollectionId", "QuoteId");

            item.Property(i => i.QuoteId)
                .IsRequired();

            item.Property(i => i.AddedAt)
                .IsRequired();
        });
    });
}
}