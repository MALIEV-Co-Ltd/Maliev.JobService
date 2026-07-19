using System.Net;
using System.Text.Json;

namespace Maliev.JobService.Api.Middleware;

/// <summary>
/// Middleware for handling validation exceptions.
/// </summary>
public class ValidationExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidationExceptionMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationExceptionMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next request delegate.</param>
    /// <param name="logger">The logger.</param>
    public ValidationExceptionMiddleware(RequestDelegate next, ILogger<ValidationExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that represents the completion of request processing.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // For now, just pass through. API Controller attribute handles 400 Bad Request automatically.
        await _next(context);
    }
}
