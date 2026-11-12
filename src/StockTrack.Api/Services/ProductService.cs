using Microsoft.EntityFrameworkCore;
using StockTrack.Api.Contracts;
using StockTrack.Api.Data;
using StockTrack.Api.Domain;
using StockTrack.Api.Errors;

namespace StockTrack.Api.Services;

public class ProductService(AppDbContext db, TimeProvider clock, ILogger<ProductService> logger)
{
    public const int MaxPageSize = 100;

    public async Task<PagedResponse<ProductResponse>> ListAsync(
        string? category, bool? lowStockOnly, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(p => p.Category == category);
        if (lowStockOnly == true)
            query = query.Where(p => p.QuantityOnHand <= p.ReorderLevel);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<ProductResponse>(items.Select(p => p.ToResponse()).ToList(), page, pageSize, total);
    }

    public async Task<ProductResponse> GetAsync(Guid id, CancellationToken ct) =>
        (await FindAsync(id, ct)).ToResponse();

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, Guid userId, CancellationToken ct)
    {
        var sku = request.Sku.Trim().ToUpperInvariant();
        if (await db.Products.AnyAsync(p => p.Sku == sku, ct))
            throw new ConflictException($"A product with SKU '{sku}' already exists.");

        var now = clock.GetUtcNow();
        var product = new Product
        {
            Sku = sku,
            Name = request.Name.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
            Price = request.Price,
            QuantityOnHand = request.InitialQuantity,
            ReorderLevel = request.ReorderLevel,
            CreatedAt = now,
            UpdatedAt = now
        };

        // Opening stock is recorded as a movement too, so the history always adds up to the current quantity.
        if (request.InitialQuantity > 0)
        {
            product.Movements.Add(new StockMovement
            {
                Change = request.InitialQuantity,
                QuantityAfter = request.InitialQuantity,
                Reason = MovementReason.Received,
                Note = "Opening stock",
                PerformedByUserId = userId,
                OccurredAt = now
            });
        }

        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Product {Sku} created by {UserId}", product.Sku, userId);
        return product.ToResponse();
    }

    public async Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        var product = await FindAsync(id, ct);
        product.Name = request.Name.Trim();
        product.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        product.Price = request.Price;
        product.ReorderLevel = request.ReorderLevel;
        product.UpdatedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        return product.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var product = await FindAsync(id, ct);
        db.Products.Remove(product);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Product {Sku} deleted", product.Sku);
    }

    private async Task<Product> FindAsync(Guid id, CancellationToken ct) =>
        await db.Products.FindAsync([id], ct) ?? throw new NotFoundException($"Product {id} was not found.");
}
