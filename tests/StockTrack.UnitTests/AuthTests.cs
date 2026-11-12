using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using StockTrack.Api.Auth;
using StockTrack.Api.Contracts;
using StockTrack.Api.Domain;
using StockTrack.Api.Errors;

namespace StockTrack.UnitTests;

public class AuthTests
{
    private static readonly JwtOptions Jwt = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "unit-test-signing-key-that-is-at-least-32-chars",
        ExpiryMinutes = 30
    };

    private static readonly DateTimeOffset Now = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    private static TokenService CreateTokenService() => new(Options.Create(Jwt), new FixedClock(Now));

    [Fact]
    public void Token_contains_only_id_and_role_and_expires_on_time()
    {
        var user = new User { Email = "staff@example.com", Role = UserRole.Staff };

        var (token, expires) = CreateTokenService().CreateToken(user);
        var jwt = new JsonWebToken(token);

        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal("Staff", jwt.GetClaim("role").Value);
        Assert.Equal("test-issuer", jwt.Issuer);
        Assert.Equal(Now.AddMinutes(30), expires);
        Assert.DoesNotContain(jwt.Claims, c => c.Value.Contains("staff@example.com"));
    }

    [Fact]
    public async Task Login_succeeds_with_correct_password_and_fails_otherwise()
    {
        await using var db = TestDb.Create();
        var auth = new AuthService(db, new PasswordHasher<User>(), CreateTokenService(), NullLogger<AuthService>.Instance);
        await auth.CreateUserAsync("Admin@Example.com ", "correct-horse-battery", UserRole.Admin, default);

        var ok = await auth.LoginAsync(new LoginRequest("admin@example.com", "correct-horse-battery"), default);
        var wrongPassword = await auth.LoginAsync(new LoginRequest("admin@example.com", "nope-nope-nope"), default);
        var unknownUser = await auth.LoginAsync(new LoginRequest("ghost@example.com", "correct-horse-battery"), default);

        Assert.NotNull(ok);
        Assert.Equal("Admin", ok.Role);
        Assert.Null(wrongPassword);
        Assert.Null(unknownUser);
    }

    [Fact]
    public async Task Passwords_are_hashed_not_stored()
    {
        await using var db = TestDb.Create();
        var auth = new AuthService(db, new PasswordHasher<User>(), CreateTokenService(), NullLogger<AuthService>.Instance);

        var user = await auth.CreateUserAsync("a@example.com", "plain-text-password", UserRole.Staff, default);

        Assert.NotEqual("plain-text-password", user.PasswordHash);
        Assert.DoesNotContain("plain-text-password", user.PasswordHash);
    }

    [Fact]
    public async Task Duplicate_email_is_rejected_case_insensitively()
    {
        await using var db = TestDb.Create();
        var auth = new AuthService(db, new PasswordHasher<User>(), CreateTokenService(), NullLogger<AuthService>.Instance);
        await auth.CreateUserAsync("a@example.com", "password-one-two", UserRole.Staff, default);

        await Assert.ThrowsAsync<ConflictException>(() =>
            auth.CreateUserAsync("A@EXAMPLE.COM", "password-one-two", UserRole.Staff, default));
    }
}
