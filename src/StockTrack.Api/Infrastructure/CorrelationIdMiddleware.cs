using System.Text.RegularExpressions;

namespace StockTrack.Api.Infrastructure;

/// <summary>
/// Gives every request a correlation id (taken from the X-Correlation-Id header or generated)
/// and adds it to the logging scope, so all log lines for one request can be found together.
/// </summary>
public partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        // Don't trust arbitrary client input in logs: only accept short, safe ids.
        var correlationId = SafeId().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await next(context);
        }
    }

    [GeneratedRegex("^[A-Za-z0-9-]{8,64}$")]
    private static partial Regex SafeId();
}
