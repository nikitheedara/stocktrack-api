using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using StockTrack.Api.Auth;
using StockTrack.Api.Data;
using StockTrack.Api.Domain;
using StockTrack.Api.Errors;
using StockTrack.Api.Infrastructure;
using StockTrack.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- Logging: JSON lines outside development so CloudWatch / log tools can query fields ----
builder.Logging.ClearProviders();
if (builder.Environment.IsDevelopment())
    builder.Logging.AddSimpleConsole(o => { o.IncludeScopes = true; o.SingleLine = true; });
else
    builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);

// ---- Configuration that must be present, checked at startup (fail fast rather than at first request) ----
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Connection string is read when the DbContext is first created (not here), so tests and
// containers can supply it through any configuration source.
builder.Services.AddDbContext<AppDbContext>((sp, o) =>
{
    var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("Connection string 'ConnectionStrings:Default' is not configured.");
    o.UseNpgsql(connectionString);
});

// ---- Application services ----
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<StockService>();

// ---- Authentication & authorisation ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = "sub",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.AdminOnly, p => p.RequireRole(nameof(UserRole.Admin)));

// Basic brute-force protection: 5 login attempts per minute per client IP.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(RateLimitPolicies.Login, ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = builder.Configuration.GetValue("RateLimiting:LoginPerMinute", 5),
            Window = TimeSpan.FromMinutes(1)
        }));
});

// ---- API plumbing ----
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(o => o.WithTitle("StockTrack API"));
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
        await ctx.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString())
        })
});

await DatabaseInitialiser.InitialiseAsync(app);
app.Run();

// Lets the integration tests start the app with WebApplicationFactory<Program>.
public partial class Program;
