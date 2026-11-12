using Microsoft.EntityFrameworkCore;
using StockTrack.Api.Contracts;
using StockTrack.Api.Data;
using StockTrack.Api.Domain;
using StockTrack.Api.Errors;

namespace StockTrack.Api.Services;

public class StockService(AppDbContext db, TimeProvider clock, ILogger<StockService> logger)
{
    public async Task<StockMovementResponse> RecordMovementAsync(
        Guid productId, RecordMovementRequest request, Guid userId, CancellationToken ct)
    {
        ValidateDirection(request.Change, request.Reason);

        var product = await db.Products.FindAsync([productId], ct)
                      ?? throw new NotFoundException($"Product {productId} was not found.");

        var newQuantity = product.QuantityOnHand + request.Change;
        if (newQuantity < 0)
            throw new BusinessRuleException(
                $"Not enough stock: {product.QuantityOnHand} on hand, tried to remove {-request.Change}.");

        var wasLow = product.IsLowStock;
        var now = clock.GetUtcNow();

        product.QuantityOnHand = newQuantity;
        product.UpdatedAt = now;

        var movement = new StockMovement
        {
            ProductId = product.Id,
            Change = request.Change,
            QuantityAfter = newQuantity,
            Reason = request.Reason,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            PerformedByUserId = userId,
            OccurredAt = now
        };
        db.StockMovements.Add(movement);

        // Quantity update and movement row are saved together; the product's row version makes a
        // concurrent change fail with a 409 rather than lose an update.
        await db.SaveChangesAsync(ct);

        if (!wasLow && product.IsLowStock)
            logger.LogWarning("Product {Sku} is now low on stock ({Quantity} left, reorder level {ReorderLevel})",
                product.Sku, product.QuantityOnHand, product.ReorderLevel);

        return movement.ToResponse();
    }

    public async Task<IReadOnlyList<StockMovementResponse>> GetHistoryAsync(Guid productId, int take, CancellationToken ct)
    {
        if (!await db.Products.AnyAsync(p => p.Id == productId, ct))
            throw new NotFoundException($"Product {productId} was not found.");

        var movements = await db.StockMovements.AsNoTracking()
            .Where(m => m.ProductId == productId)
            .OrderByDescending(m => m.OccurredAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(ct);

        return movements.Select(m => m.ToResponse()).ToList();
    }

    /// <summary>Received/Returned must add stock, Sold/Damaged must remove it. Adjustments can go either way.</summary>
    public static void ValidateDirection(int change, MovementReason reason)
    {
        if (change == 0)
            throw new BusinessRuleException("Change must not be zero.");

        var ok = reason switch
        {
            MovementReason.Received or MovementReason.Returned => change > 0,
            MovementReason.Sold or MovementReason.Damaged => change < 0,
            MovementReason.Adjustment => true,
            _ => false
        };

        if (!ok)
            throw new BusinessRuleException($"A '{reason}' movement can't have a change of {change}.");
    }
}
