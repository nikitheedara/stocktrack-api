using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using StockTrack.Api.Auth;
using StockTrack.Api.Contracts;

namespace StockTrack.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    /// <summary>Exchange an email and password for a JWT access token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Login)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await auth.LoginAsync(request, ct);
        // Same response for "no such user" and "wrong password".
        return result is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.")
            : Ok(result);
    }

    /// <summary>Create a staff or admin account. Admins only: there's no public sign-up.</summary>
    [HttpPost("users")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        var user = await auth.CreateUserAsync(request.Email, request.Password, request.Role, ct);
        return StatusCode(StatusCodes.Status201Created, user.ToResponse());
    }
}
