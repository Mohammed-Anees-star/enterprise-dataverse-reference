using EnterpriseTicketing.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior that provides structured request/response logging.
/// Logs at Debug level in normal operation to avoid production log noise.
/// Critical for distributed tracing: logs request name, user, and correlation context.
///
/// Enterprise note: Do NOT log request body contents for sensitive operations
/// (password changes, financial data). Use request markers or log allowlists.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<TRequest> logger,
    ICurrentUserService currentUserService) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = currentUserService.UserId ?? "anonymous";

        logger.LogDebug("Handling {RequestName} for user {UserId}", requestName, userId);

        var response = await next(cancellationToken);

        logger.LogDebug("Handled {RequestName} for user {UserId}", requestName, userId);

        return response;
    }
}
