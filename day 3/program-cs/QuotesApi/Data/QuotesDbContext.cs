using Microsoft.EntityFrameworkCore;
using QuotesApi.Models;
 
namespace QuotesApi.Data;
 
public class QuotesDbContext : DbContext
{
    public QuotesDbContext(DbContextOptions options)
        : base(options)
    {
    }
 
    public DbSet<Quote> Quotes { get; set; }
    public DbSet<Collection> Collections { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
 
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Quote>(entity =>
        {
            entity.HasKey(q => q.Id);
 
            entity.Property(q => q.Id)
                .ValueGeneratedOnAdd();

            entity.HasIndex(q => new { q.Author, q.IsDeleted })
                .HasDatabaseName("IX_Quotes_Author_IsDeleted");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
 
            entity.Property(u => u.Id)
                .ValueGeneratedOnAdd();
        });
 
        modelBuilder.Entity<Collection>(entity =>
        {
            entity.HasKey(c => c.Id);
 
            entity.Property(c => c.Id)
                .ValueGeneratedOnAdd();
 
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
 
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(r => r.Id);
 
            entity.Property(r => r.Id)
                .ValueGeneratedOnAdd();
 
            entity.Property(r => r.Token)
                .IsRequired();
 
            entity.Property(r => r.UserId)
                .IsRequired();
 
            entity.Property(r => r.ExpiresAt)
                .IsRequired();
        });
    }
}
