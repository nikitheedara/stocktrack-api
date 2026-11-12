using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StockTrack.Api.Contracts;
using StockTrack.Api.Data;
using StockTrack.Api.Errors;
using StockTrack.Api.Services;

namespace StockTrack.UnitTests;

public class ProductServiceTests
{
    private static readonly Guid AdminId = Guid.NewGuid();

    private static ProductService CreateService(AppDbContext db) =>
        new(db, new FixedClock(DateTimeOffset.UtcNow), NullLogger<ProductService>.Instance);

    [Fact]
    public async Task Create_normalises_sku_and_records_opening_stock()
    {
        await using var db = TestDb.Create();

        var created = await CreateService(db).CreateAsync(
            new CreateProductRequest(" bread-wm-700 ", "Wholemeal Bread 700g", "Bakery", 4.50m, 12, 4), AdminId, default);

        Assert.Equal("BREAD-WM-700", created.Sku);
        var movement = await db.StockMovements.SingleAsync();
        Assert.Equal(12, movement.Change);
        Assert.Equal("Opening stock", movement.Note);
    }

    [Fact]
    public async Task Create_with_zero_stock_records_no_movement()
    {
        await using var db = TestDb.Create();

        await CreateService(db).CreateAsync(new CreateProductRequest("EGGS-12", "Eggs 12pk", null, 6m, 0, 5), AdminId, default);

        Assert.Empty(db.StockMovements);
    }

    [Fact]
    public async Task Duplicate_sku_is_a_conflict()
    {
        await using var db = TestDb.Create();
        var service = CreateService(db);
        await service.CreateAsync(new CreateProductRequest("EGGS-12", "Eggs 12pk", null, 6m, 0, 5), AdminId, default);

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(new CreateProductRequest("eggs-12", "Other eggs", null, 7m, 0, 5), AdminId, default));
    }

    [Fact]
    public async Task List_filters_low_stock_and_clamps_page_size()
    {
        await using var db = TestDb.Create();
        await TestDb.AddProductAsync(db, quantity: 1, reorderLevel: 5);   // low
        await TestDb.AddProductAsync(db, quantity: 50, reorderLevel: 5);  // fine

        var result = await CreateService(db).ListAsync(null, lowStockOnly: true, page: 0, pageSize: 5000, default);

        Assert.Single(result.Items);
        Assert.Equal(1, result.Page);
        Assert.Equal(ProductService.MaxPageSize, result.PageSize);
    }

    [Fact]
    public async Task Get_missing_product_throws_not_found()
    {
        await using var db = TestDb.Create();
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(db).GetAsync(Guid.NewGuid(), default));
    }
}
