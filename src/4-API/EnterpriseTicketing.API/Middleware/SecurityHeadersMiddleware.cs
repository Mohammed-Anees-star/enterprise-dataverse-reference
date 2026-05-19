namespace EnterpriseTicketing.API.Middleware;

/// <summary>
/// Adds security headers to all API responses.
/// These headers are a first line of defense against common web attacks.
/// Review and adjust Content-Security-Policy if Swagger UI is served from the same origin.
/// </summary>
public sealed class SecurityHeadersMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";
        context.Response.Headers["Permissions-Policy"] = "accelerometer=(), camera=(), geolocation=(), microphone=()";

        // Remove server identification headers — obscurity is a minor security benefit
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");
        context.Response.Headers.Remove("X-AspNet-Version");

        await next(context);
    }
}
