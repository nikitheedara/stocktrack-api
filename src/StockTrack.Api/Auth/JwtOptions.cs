using System.ComponentModel.DataAnnotations;

namespace StockTrack.Api.Auth;

public class JwtOptions
{
    public const string Section = "Jwt";

    [Required] public string Issuer { get; set; } = "stocktrack-api";
    [Required] public string Audience { get; set; } = "stocktrack-clients";

    // Never stored in appsettings.json: comes from user-secrets locally and environment variables / a
    // secrets manager when deployed. 32+ characters = at least 256 bits for HMAC-SHA256.
    [Required, MinLength(32)] public string SigningKey { get; set; } = string.Empty;

    [Range(5, 240)] public int ExpiryMinutes { get; set; } = 60;
}
