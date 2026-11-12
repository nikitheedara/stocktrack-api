using Microsoft.EntityFrameworkCore;
using StockTrack.Api.Auth;
using StockTrack.Api.Domain;

namespace StockTrack.Api.Data;

public static class DatabaseInitialiser
{
    /// <summary>
    /// Applies migrations (when enabled) and creates the first admin account if the database has no users.
    /// The admin's details come from configuration (env vars / user-secrets), never from source code.
    /// </summary>
    public static async Task InitialiseAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitialiser");
        var db = services.GetRequiredService<AppDbContext>();

        if (config.GetValue("Database:MigrateOnStartup", false))
        {
            logger.LogInformation("Applying database migrations");
            await db.Database.MigrateAsync();
        }

        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        if (await db.Users.AnyAsync())
            return;

        await services.GetRequiredService<AuthService>()
            .CreateUserAsync(email, password, UserRole.Admin, CancellationToken.None);
        logger.LogInformation("Seeded initial admin account");
    }
}
