using System.Security.Claims;

namespace StockTrack.Api.Auth;

public static class AuthPolicies
{
    public const string AdminOnly = "AdminOnly";
}

public static class RateLimitPolicies
{
    public const string Login = "login";
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sub"), out var id)
            ? id
            : throw new InvalidOperationException("Authenticated user has no valid 'sub' claim.");
}
