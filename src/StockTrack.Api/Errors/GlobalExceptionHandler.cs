using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace StockTrack.Api.Errors;

/// <summary>
/// Turns exceptions into RFC 7807 problem responses. Unexpected errors are logged in full
/// but the client only gets a generic message, so stack traces and SQL never leak out.
/// </summary>
public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetails)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException e => (StatusCodes.Status404NotFound, "Not found", e.Message),
            ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Message),
            BusinessRuleException e => (StatusCodes.Status422UnprocessableEntity, "Business rule violated", e.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflict",
                "The record was changed by someone else. Reload it and try again."),
            _ => (StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred.")
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            logger.LogInformation("Request failed with {Status}: {Message}", status, exception.Message);

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}
