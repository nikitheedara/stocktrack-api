using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using StockTrack.Api.Contracts;
using StockTrack.Api.Domain;
using Testcontainers.PostgreSql;

namespace StockTrack.IntegrationTests;

/// <summary>
/// Starts a throwaway PostgreSQL container and runs the real API against it, so the tests cover
/// the actual database, migrations, auth pipeline and JSON serialisation.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@stocktrack.test";
    public const string AdminPassword = "integration-admin-password";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-at-least-32-characters");
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
        builder.UseSetting("RateLimiting:LoginPerMinute", "1000");
    }

    public async Task InitializeAsync() => await _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public async Task<HttpClient> CreateClientAsAsync(string email, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonDefaults.Options);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    public Task<HttpClient> CreateAdminClientAsync() => CreateClientAsAsync(AdminEmail, AdminPassword);

    public async Task<HttpClient> CreateStaffClientAsync()
    {
        var admin = await CreateAdminClientAsync();
        var email = $"staff-{Guid.NewGuid():N}@stocktrack.test";
        const string password = "staff-member-password";
        var created = await admin.PostAsJsonAsync("/api/auth/users",
            new CreateUserRequest(email, password, UserRole.Staff), JsonDefaults.Options);
        created.EnsureSuccessStatusCode();
        return await CreateClientAsAsync(email, password);
    }
}

[CollectionDefinition(Name)]
public class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
