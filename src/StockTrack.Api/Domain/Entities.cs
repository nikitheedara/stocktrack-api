namespace StockTrack.Api.Domain;

public enum UserRole
{
    Staff = 0,
    Admin = 1
}

public enum MovementReason
{
    Received = 0,
    Sold = 1,
    Adjustment = 2,
    Damaged = 3,
    Returned = 4
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Only the minimum personal data needed to sign in is stored (no names, phone numbers, etc.).
    public required string Email { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Staff;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public string? Category { get; set; }
    public decimal Price { get; set; }
    public int QuantityOnHand { get; set; }
    public int ReorderLevel { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Optimistic concurrency token so two staff members can't silently overwrite each other's stock count.
    public uint Version { get; set; }

    public List<StockMovement> Movements { get; set; } = [];

    public bool IsLowStock => QuantityOnHand <= ReorderLevel;
}

public class StockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public int Change { get; set; }
    public int QuantityAfter { get; set; }
    public MovementReason Reason { get; set; }
    public string? Note { get; set; }
    public Guid PerformedByUserId { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
