using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using StockTrack.Api.Contracts;
using StockTrack.Api.Domain;

namespace StockTrack.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ApiTests(ApiFactory factory)
{
    private static string NewSku() => "SKU-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static async Task<ProductResponse> CreateProductAsync(HttpClient admin, int quantity = 10, int reorderLevel = 3)
    {
        var response = await admin.PostAsJsonAsync("/api/products",
            new CreateProductRequest(NewSku(), "Test product", "Pantry", 2.50m, quantity, reorderLevel), JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductResponse>(JsonDefaults.Options))!;
    }

    [Fact]
    public async Task Health_endpoint_reports_database_healthy()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Products_require_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_returns_401_without_saying_which_part_was_wrong()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ApiFactory.AdminEmail, "definitely-wrong"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Invalid email or password.", problem!.Title);
    }

    [Fact]
    public async Task Tampered_token_is_rejected()
    {
        var client = await factory.CreateAdminClientAsync();
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", token[..^4] + "AAAA");

        var response = await client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Staff_cannot_create_products_or_users()
    {
        var staff = await factory.CreateStaffClientAsync();

        var product = await staff.PostAsJsonAsync("/api/products",
            new CreateProductRequest(NewSku(), "Nope", null, 1m, 0, 0), JsonDefaults.Options);
        var user = await staff.PostAsJsonAsync("/api/auth/users",
            new CreateUserRequest("someone@stocktrack.test", "long-enough-password", UserRole.Admin), JsonDefaults.Options);

        Assert.Equal(HttpStatusCode.Forbidden, product.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, user.StatusCode);
    }

    [Fact]
    public async Task Invalid_product_returns_validation_problem()
    {
        var admin = await factory.CreateAdminClientAsync();

        var response = await admin.PostAsJsonAsync("/api/products",
            new CreateProductRequest("bad sku!", "", null, -1m, 0, 0), JsonDefaults.Options);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("Sku", problem!.Errors.Keys);
        Assert.Contains("Price", problem.Errors.Keys);
    }

    [Fact]
    public async Task Duplicate_sku_returns_409()
    {
        var admin = await factory.CreateAdminClientAsync();
        var sku = NewSku();
        var request = new CreateProductRequest(sku, "First", null, 1m, 0, 0);

        await admin.PostAsJsonAsync("/api/products", request, JsonDefaults.Options);
        var second = await admin.PostAsJsonAsync("/api/products", request, JsonDefaults.Options);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Staff_can_sell_stock_and_history_is_recorded()
    {
        var admin = await factory.CreateAdminClientAsync();
        var staff = await factory.CreateStaffClientAsync();
        var product = await CreateProductAsync(admin, quantity: 10, reorderLevel: 3);

        var sale = await staff.PostAsJsonAsync($"/api/products/{product.Id}/movements",
            new RecordMovementRequest(-8, MovementReason.Sold, "Weekend sale"), JsonDefaults.Options);
        Assert.Equal(HttpStatusCode.Created, sale.StatusCode);

        var updated = await staff.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}", JsonDefaults.Options);
        Assert.Equal(2, updated!.QuantityOnHand);
        Assert.True(updated.IsLowStock);

        var history = await staff.GetFromJsonAsync<List<StockMovementResponse>>(
            $"/api/products/{product.Id}/movements", JsonDefaults.Options);
        Assert.Equal(2, history!.Count); // opening stock + sale
        Assert.Equal("Sold", history[0].Reason);
    }

    [Fact]
    public async Task Overselling_returns_422_and_leaves_stock_unchanged()
    {
        var admin = await factory.CreateAdminClientAsync();
        var product = await CreateProductAsync(admin, quantity: 1);

        var response = await admin.PostAsJsonAsync($"/api/products/{product.Id}/movements",
            new RecordMovementRequest(-5, MovementReason.Sold, null), JsonDefaults.Options);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var after = await admin.GetFromJsonAsync<ProductResponse>($"/api/products/{product.Id}", JsonDefaults.Options);
        Assert.Equal(1, after!.QuantityOnHand);
    }

    [Fact]
    public async Task Low_stock_filter_only_returns_low_items()
    {
        var admin = await factory.CreateAdminClientAsync();
        var low = await CreateProductAsync(admin, quantity: 1, reorderLevel: 5);
        var fine = await CreateProductAsync(admin, quantity: 50, reorderLevel: 5);

        var page = await admin.GetFromJsonAsync<PagedResponse<ProductResponse>>(
            "/api/products?lowStock=true&pageSize=100", JsonDefaults.Options);

        Assert.Contains(page!.Items, p => p.Id == low.Id);
        Assert.DoesNotContain(page.Items, p => p.Id == fine.Id);
    }

    [Fact]
    public async Task Correlation_id_is_echoed_back()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "test-correlation-123");

        var response = await client.GetAsync("/health");

        Assert.Equal("test-correlation-123", response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task Missing_product_returns_404_problem()
    {
        var admin = await factory.CreateAdminClientAsync();

        var response = await admin.GetAsync($"/api/products/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
