using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StockTrack.Api.Contracts;
using StockTrack.Api.Data;
using StockTrack.Api.Domain;
using StockTrack.Api.Errors;

namespace StockTrack.Api.Auth;

public class AuthService(
    AppDbContext db,
    IPasswordHasher<User> hasher,
    ITokenService tokens,
    ILogger<AuthService> logger)
{
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = Normalise(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            // Hash anyway so a missing account takes about as long as a wrong password
            // (makes it harder to find out which emails have accounts by timing responses).
            hasher.HashPassword(new User { Email = email }, request.Password);
            logger.LogWarning("Failed login: unknown account");
            return null;
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            logger.LogWarning("Failed login for user {UserId}", user.Id);
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(ct);
        }

        var (token, expires) = tokens.CreateToken(user);
        logger.LogInformation("User {UserId} signed in", user.Id);
        return new LoginResponse(token, expires, user.Role.ToString());
    }

    public async Task<User> CreateUserAsync(string email, string password, UserRole role, CancellationToken ct)
    {
        email = Normalise(email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("An account with that email already exists.");

        var user = new User { Email = email, Role = role };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created {Role} user {UserId}", role, user.Id);
        return user;
    }

    public static string Normalise(string email) => email.Trim().ToLowerInvariant();
}
