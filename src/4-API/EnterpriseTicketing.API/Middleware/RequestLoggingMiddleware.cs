using System.Diagnostics;

namespace EnterpriseTicketing.API.Middleware;

/// <summary>
/// Custom request-logging middleware. UseSerilogRequestLogging covers the standard case;
/// this middleware is registered for environments where the Serilog request-logging
/// integration is unavailable (e.g., a minimal Microsoft.Extensions.Logging configuration)
/// or when callers want raw ILogger output in addition to Serilog.
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "HTTP {Method} {Path} → {StatusCode} in {ElapsedMs}ms (correlation: {CorrelationId})",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                context.Items["CorrelationId"]);
        }
    }
}
