using Microsoft.EntityFrameworkCore;
using StockTrack.Api.Data;
using StockTrack.Api.Domain;

namespace StockTrack.UnitTests;

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class TestDb
{
    public static AppDbContext Create() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public static async Task<Product> AddProductAsync(AppDbContext db, int quantity = 10, int reorderLevel = 3)
    {
        var product = new Product
        {
            Sku = "MILK-2L-" + Random.Shared.Next(1000, 9999),
            Name = "Full Cream Milk 2L",
            Category = "Dairy",
            Price = 3.10m,
            QuantityOnHand = quantity,
            ReorderLevel = reorderLevel
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return product;
    }
}
