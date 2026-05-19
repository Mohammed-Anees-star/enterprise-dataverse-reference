using Serilog.Context;

namespace EnterpriseTicketing.API.Middleware;

/// <summary>
/// Extracts or generates a correlation ID for each request.
/// Adds it to the response headers and Serilog log context.
///
/// Why correlation IDs matter in production:
///   When a user reports "my request failed at 2:43pm", you can filter all logs
///   (API, background service, Functions, Azure) by the single correlation ID
///   to reconstruct the full request trace across all services.
///
/// Header: X-Correlation-ID (inbound and outbound)
/// Enricher: CorrelationId added to every log entry via Serilog LogContext
/// </summary>
public sealed class CorrelationIdMiddleware : IMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var correlationId = context.Request.Headers.TryGetValue(CorrelationIdHeader, out var headerValue)
            && !string.IsNullOrWhiteSpace(headerValue)
            ? headerValue.ToString()
            : Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        // Push correlation ID into Serilog's LogContext — all log entries in this request will carry it
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }
}
