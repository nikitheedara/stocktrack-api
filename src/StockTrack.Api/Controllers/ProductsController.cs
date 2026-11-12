using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StockTrack.Api.Auth;
using StockTrack.Api.Contracts;
using StockTrack.Api.Services;

namespace StockTrack.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController(ProductService products, StockService stock) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<ProductResponse>> List(
        [FromQuery] string? category,
        [FromQuery] bool? lowStock,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        products.ListAsync(category, lowStock, page, pageSize, ct);

    [HttpGet("{id:guid}")]
    public Task<ProductResponse> Get(Guid id, CancellationToken ct) => products.GetAsync(id, ct);

    [HttpPost]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken ct)
    {
        var created = await products.CreateAsync(request, User.GetUserId(), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public Task<ProductResponse> Update(Guid id, UpdateProductRequest request, CancellationToken ct) =>
        products.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await products.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Record stock coming in or going out. Any signed-in staff member can do this.</summary>
    [HttpPost("{id:guid}/movements")]
    [ProducesResponseType<StockMovementResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordMovement(Guid id, RecordMovementRequest request, CancellationToken ct)
    {
        var movement = await stock.RecordMovementAsync(id, request, User.GetUserId(), ct);
        return StatusCode(StatusCodes.Status201Created, movement);
    }

    [HttpGet("{id:guid}/movements")]
    public Task<IReadOnlyList<StockMovementResponse>> History(Guid id, [FromQuery] int take = 50, CancellationToken ct = default) =>
        stock.GetHistoryAsync(id, take, ct);
}
