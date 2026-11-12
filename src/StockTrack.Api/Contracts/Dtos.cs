using System.ComponentModel.DataAnnotations;
using StockTrack.Api.Domain;

namespace StockTrack.Api.Contracts;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, string Role);

public record CreateUserRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(12), MaxLength(128)] string Password,
    UserRole Role = UserRole.Staff);

public record UserResponse(Guid Id, string Email, string Role, DateTimeOffset CreatedAt);

public record CreateProductRequest(
    [Required, RegularExpression("^[A-Z0-9-]{3,32}$", ErrorMessage = "SKU must be 3-32 characters: A-Z, 0-9 or '-'.")] string Sku,
    [Required, MaxLength(200)] string Name,
    [MaxLength(100)] string? Category,
    [Range(0, 100000)] decimal Price,
    [Range(0, 1000000)] int InitialQuantity,
    [Range(0, 100000)] int ReorderLevel);

public record UpdateProductRequest(
    [Required, MaxLength(200)] string Name,
    [MaxLength(100)] string? Category,
    [Range(0, 100000)] decimal Price,
    [Range(0, 100000)] int ReorderLevel);

public record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    string? Category,
    decimal Price,
    int QuantityOnHand,
    int ReorderLevel,
    bool IsLowStock,
    DateTimeOffset UpdatedAt);

public record RecordMovementRequest(
    [Range(-100000, 100000)] int Change,
    [Required] MovementReason Reason,
    [MaxLength(500)] string? Note);

public record StockMovementResponse(
    Guid Id,
    int Change,
    int QuantityAfter,
    string Reason,
    string? Note,
    Guid PerformedByUserId,
    DateTimeOffset OccurredAt);

public record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class Mapping
{
    public static ProductResponse ToResponse(this Product p) =>
        new(p.Id, p.Sku, p.Name, p.Category, p.Price, p.QuantityOnHand, p.ReorderLevel, p.IsLowStock, p.UpdatedAt);

    public static StockMovementResponse ToResponse(this StockMovement m) =>
        new(m.Id, m.Change, m.QuantityAfter, m.Reason.ToString(), m.Note, m.PerformedByUserId, m.OccurredAt);

    public static UserResponse ToResponse(this User u) =>
        new(u.Id, u.Email, u.Role.ToString(), u.CreatedAt);
}
