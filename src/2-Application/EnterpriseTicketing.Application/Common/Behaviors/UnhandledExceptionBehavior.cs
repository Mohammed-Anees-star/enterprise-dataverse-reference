using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior that catches and logs unhandled exceptions from request handlers.
/// Placed as the outermost behavior so it wraps all others.
/// Re-throws after logging to let middleware handle the HTTP response.
///
/// Enterprise note: Logging here (not just in middleware) provides the MediatR context
/// (request type, request data) which is invaluable for production debugging.
/// </summary>
public sealed class UnhandledExceptionBehavior<TRequest, TResponse>(
    ILogger<TRequest> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken);
        }
        catch (Exception ex)
        {
            var requestName = typeof(TRequest).Name;

            logger.LogError(ex,
                "Unhandled exception for request {RequestName}: {@Request}",
                requestName, request);

            throw;
        }
    }
}
