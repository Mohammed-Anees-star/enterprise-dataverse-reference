using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Timeout;

namespace EnterpriseTicketing.Infrastructure.Http;

/// <summary>
/// Centralized Polly resilience policy definitions.
///
/// Enterprise resilience strategy for Dataverse HTTP calls:
///
/// 1. TIMEOUT: Give up after 30 seconds. Prevents thread starvation from hung connections.
///
/// 2. RETRY with exponential backoff + jitter:
///    Retry 3 times on transient failures (5xx, 429, network errors).
///    Jitter (0-1000ms random) prevents retry storms when many clients hit the same failure.
///    Exponential backoff: 2s, 4s, 8s between retries.
///
/// 3. CIRCUIT BREAKER:
///    After 50% failure rate over 10 requests, break for 30 seconds.
///    Prevents hammering a failing Dataverse instance.
///    In half-open state, allows one test request to check recovery.
///
/// Policy wrap order: Timeout → Retry → CircuitBreaker
/// Each layer wraps the inner one. Timeout fires first, then retry, then circuit breaker.
/// </summary>
public static class PolicyDefinitions
{
    public static IAsyncPolicy<HttpResponseMessage> GetDataverseRetryPolicy(ILogger logger)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: (attempt, response, context) =>
                {
                    // Honor Retry-After header from Dataverse (429 responses)
                    if (response.Result?.Headers.RetryAfter?.Delta is { } retryAfter)
                        return retryAfter;

                    var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                    var jitter = TimeSpan.FromMilliseconds(new Random().Next(0, 1000));
                    return baseDelay + jitter;
                },
                onRetryAsync: (outcome, timespan, attempt, context) =>
                {
                    logger.LogWarning(
                        "Dataverse HTTP retry {Attempt}/3 after {Delay}ms. Status: {StatusCode}",
                        attempt, timespan.TotalMilliseconds, outcome.Result?.StatusCode);
                    return Task.CompletedTask;
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetDataverseCircuitBreakerPolicy(ILogger logger)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: 0.5,       // Break when 50% of requests fail
                samplingDuration: TimeSpan.FromSeconds(10),
                minimumThroughput: 5,        // Need at least 5 requests to evaluate
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, state, duration, context) =>
                {
                    logger.LogError(
                        "Dataverse circuit breaker OPENED for {Duration}s. Last status: {StatusCode}",
                        duration.TotalSeconds, outcome.Result?.StatusCode);
                },
                onReset: context =>
                {
                    logger.LogInformation("Dataverse circuit breaker CLOSED — service recovered.");
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation("Dataverse circuit breaker HALF-OPEN — testing recovery.");
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetDataverseTimeoutPolicy()
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(
            seconds: 30,
            timeoutStrategy: TimeoutStrategy.Optimistic);
    }

    public static IAsyncPolicy<HttpResponseMessage> GetDataversePolicy(ILogger logger)
    {
        return Policy.WrapAsync(
            GetDataverseTimeoutPolicy(),
            GetDataverseRetryPolicy(logger),
            GetDataverseCircuitBreakerPolicy(logger));
    }
}
