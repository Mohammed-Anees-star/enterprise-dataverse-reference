using System.Diagnostics;
using EnterpriseTicketing.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior that monitors handler execution time and logs slow requests.
/// In production, this surfaces SLA breaches before they become user-visible problems.
///
/// Thresholds:
///   > 500ms: Warning — investigate for N+1 queries, missing indexes, or inefficient FetchXML
///   > 2000ms: Error — immediate action required, likely SLA breach
///
/// These thresholds should be tuned to your SLAs. Consider externalizing them to configuration.
/// </summary>
public sealed class PerformanceBehavior<TRequest, TResponse>(
    ILogger<TRequest> logger,
    ICurrentUserService currentUserService) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int WarningThresholdMs = 500;
    private const int ErrorThresholdMs = 2000;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();

        var response = await next(cancellationToken);

        timer.Stop();
        var elapsedMs = timer.ElapsedMilliseconds;

        if (elapsedMs >= WarningThresholdMs)
        {
            var requestName = typeof(TRequest).Name;
            var userId = currentUserService.UserId ?? "anonymous";

            if (elapsedMs >= ErrorThresholdMs)
            {
                logger.LogError(
                    "Long-running request detected. Name: {RequestName} | ElapsedMs: {ElapsedMs} | UserId: {UserId} | Request: {@Request}",
                    requestName, elapsedMs, userId, request);
            }
            else
            {
                logger.LogWarning(
                    "Slow request detected. Name: {RequestName} | ElapsedMs: {ElapsedMs} | UserId: {UserId}",
                    requestName, elapsedMs, userId);
            }
        }

        return response;
    }
}
