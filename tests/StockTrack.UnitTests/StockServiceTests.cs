using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using StockTrack.Api.Contracts;
using StockTrack.Api.Domain;
using StockTrack.Api.Errors;
using StockTrack.Api.Services;

namespace StockTrack.UnitTests;

public class StockServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid StaffId = Guid.NewGuid();

    private static StockService CreateService(Api.Data.AppDbContext db) =>
        new(db, new FixedClock(Now), NullLogger<StockService>.Instance);

    [Fact]
    public async Task Selling_stock_reduces_quantity_and_records_movement()
    {
        await using var db = TestDb.Create();
        var product = await TestDb.AddProductAsync(db, quantity: 10);

        var result = await CreateService(db).RecordMovementAsync(
            product.Id, new RecordMovementRequest(-4, MovementReason.Sold, "Till 3"), StaffId, CancellationToken.None);

        Assert.Equal(6, result.QuantityAfter);
        Assert.Equal(StaffId, result.PerformedByUserId);
        Assert.Equal(Now, result.OccurredAt);

        var saved = await db.Products.Include(p => p.Movements).SingleAsync();
        Assert.Equal(6, saved.QuantityOnHand);
        Assert.Single(saved.Movements);
    }

    [Fact]
    public async Task Cannot_remove_more_stock_than_is_on_hand()
    {
        await using var db = TestDb.Create();
        var product = await TestDb.AddProductAsync(db, quantity: 2);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService(db).RecordMovementAsync(
            product.Id, new RecordMovementRequest(-5, MovementReason.Sold, null), StaffId, CancellationToken.None));

        Assert.Contains("Not enough stock", ex.Message);
        Assert.Equal(2, (await db.Products.SingleAsync()).QuantityOnHand);
        Assert.Empty(db.StockMovements);
    }

    [Fact]
    public async Task Unknown_product_throws_not_found()
    {
        await using var db = TestDb.Create();

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(db).RecordMovementAsync(
            Guid.NewGuid(), new RecordMovementRequest(5, MovementReason.Received, null), StaffId, CancellationToken.None));
    }

    [Fact]
    public async Task History_is_returned_newest_first()
    {
        await using var db = TestDb.Create();
        var product = await TestDb.AddProductAsync(db, quantity: 10);
        var clock = new FixedClock(Now);
        var service = new StockService(db, clock, NullLogger<StockService>.Instance);

        await service.RecordMovementAsync(product.Id, new(5, MovementReason.Received, "first"), StaffId, default);
        clock.Now = Now.AddHours(1);
        await service.RecordMovementAsync(product.Id, new(-1, MovementReason.Damaged, "second"), StaffId, default);

        var history = await service.GetHistoryAsync(product.Id, 50, default);

        Assert.Equal(["second", "first"], history.Select(h => h.Note));
    }

    [Theory]
    [InlineData(5, MovementReason.Received)]
    [InlineData(1, MovementReason.Returned)]
    [InlineData(-3, MovementReason.Sold)]
    [InlineData(-1, MovementReason.Damaged)]
    [InlineData(7, MovementReason.Adjustment)]
    [InlineData(-7, MovementReason.Adjustment)]
    public void Valid_directions_are_accepted(int change, MovementReason reason) =>
        StockService.ValidateDirection(change, reason);

    [Theory]
    [InlineData(-5, MovementReason.Received)]
    [InlineData(3, MovementReason.Sold)]
    [InlineData(2, MovementReason.Damaged)]
    [InlineData(0, MovementReason.Adjustment)]
    public void Invalid_directions_are_rejected(int change, MovementReason reason) =>
        Assert.Throws<BusinessRuleException>(() => StockService.ValidateDirection(change, reason));

    [Theory]
    [InlineData(4, 3, false)]
    [InlineData(3, 3, true)]
    [InlineData(0, 3, true)]
    public void Low_stock_is_at_or_below_reorder_level(int quantity, int reorderLevel, bool expected)
    {
        var product = new Product { Sku = "X-1", Name = "X", QuantityOnHand = quantity, ReorderLevel = reorderLevel };
        Assert.Equal(expected, product.IsLowStock);
    }
}
