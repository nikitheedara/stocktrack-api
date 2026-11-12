using Microsoft.EntityFrameworkCore;
using StockTrack.Api.Domain;

namespace StockTrack.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(256);
            e.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.HasIndex(p => p.Sku).IsUnique();
            e.Property(p => p.Sku).HasMaxLength(32);
            e.Property(p => p.Name).HasMaxLength(200);
            e.Property(p => p.Category).HasMaxLength(100);
            e.Property(p => p.Price).HasPrecision(10, 2);
            e.Ignore(p => p.IsLowStock);

            // Postgres' built-in xmin column works as a row version. Other providers (the in-memory
            // one used by unit tests) just get a normal concurrency column.
            if (Database.IsNpgsql())
                e.Property(p => p.Version).IsRowVersion();
            else
                e.Property(p => p.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<StockMovement>(e =>
        {
            e.HasOne(m => m.Product)
                .WithMany(p => p.Movements)
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(m => m.Reason).HasConversion<string>().HasMaxLength(20);
            e.Property(m => m.Note).HasMaxLength(500);
            e.HasIndex(m => new { m.ProductId, m.OccurredAt });
        });
    }
}
