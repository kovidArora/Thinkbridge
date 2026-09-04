using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.RateLimiting;
using QuotesApi.Services;
using System.Threading.RateLimiting;

namespace QuotesApi.Extensions;

/// Tunables for the entra-metadata resilience pipeline. Defaults are the real
/// production values; tests override the durations to short ones so a
/// circuit-open-and-recover test runs in milliseconds instead of minutes.
public sealed class EntraResilienceOptions
{
    public int MaxRetryAttempts { get; init; } = 3;
    public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);
    public DelayBackoffType RetryBackoffType { get; init; } = DelayBackoffType.Exponential;
    public double CircuitBreakerFailureRatio { get; init; } = 0.5;
    public int CircuitBreakerMinimumThroughput { get; init; } = 4;
    public TimeSpan CircuitBreakerSamplingDuration { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan CircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(10);
    public int BulkheadMaxConcurrency { get; init; } = 10;
    public int BulkheadQueueLimit { get; init; } = 0;
}

public static class ResilientHttpClientExtensions
{
    public static bool IsIdempotent(HttpMethod method) =>
        method == HttpMethod.Get || method == HttpMethod.Head ||
        method == HttpMethod.Put || method == HttpMethod.Delete || method == HttpMethod.Options;

    public static IHttpClientBuilder AddEntraMetadataClient(
        this IServiceCollection services,
        EntraResilienceOptions? resilienceOptions = null)
    {
        var options = resilienceOptions ?? new EntraResilienceOptions();
        var httpClientBuilder = services.AddHttpClient<IEntraMetadataClient, EntraMetadataClient>("entra-metadata");

        httpClientBuilder.AddResilienceHandler("default", (resilienceBuilder, context) =>
            {
                var logger = context.ServiceProvider.GetRequiredService<ILogger<EntraMetadataClient>>();

                // Bulkhead first (outermost): caps how many calls to this
                // dependency can be in flight at once — including retries —
                // so a slow/hanging dependency can't let unbounded concurrent
                // callers pile up and exhaust this process's own resources.
                resilienceBuilder.AddConcurrencyLimiter(new ConcurrencyLimiterOptions
                {
                    PermitLimit = options.BulkheadMaxConcurrency,
                    QueueLimit = options.BulkheadQueueLimit,
                });

                resilienceBuilder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = options.MaxRetryAttempts,
                    BackoffType = options.RetryBackoffType,
                    Delay = options.RetryDelay,
                    UseJitter = true,
                    ShouldHandle = args =>
                    {
                        // Never retry a non-idempotent request (e.g. a POST) —
                        // retrying one that already reached the server risks
                        // running it twice. Every call this client makes today
                        // is a GET, but this guard makes that a hard rule
                        // rather than an accident of what the client happens
                        // to do right now.
                        var method = args.Outcome.Result?.RequestMessage?.Method;
                        if (method is not null && !IsIdempotent(method))
                        {
                            return ValueTask.FromResult(false);
                        }

                        return ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome));
                    },
                    OnRetry = args =>
                    {
                        logger.LogWarning(
                            "Retry {AttemptNumber}/{MaxAttempts} for entra-metadata after {Delay}ms, reason: {Reason}",
                            args.AttemptNumber + 1,
                            options.MaxRetryAttempts,
                            args.RetryDelay.TotalMilliseconds,
                            args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                        return ValueTask.CompletedTask;
                    }
                });

                resilienceBuilder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = options.CircuitBreakerFailureRatio,
                    SamplingDuration = options.CircuitBreakerSamplingDuration,
                    MinimumThroughput = options.CircuitBreakerMinimumThroughput,
                    BreakDuration = options.CircuitBreakerBreakDuration,
                    OnOpened = args =>
                    {
                        logger.LogError(
                            "Circuit breaker OPENED for entra-metadata client; will stay open for {BreakDuration}ms, reason: {Reason}",
                            args.BreakDuration.TotalMilliseconds,
                            args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                        return ValueTask.CompletedTask;
                    },
                    OnHalfOpened = args =>
                    {
                        logger.LogWarning("Circuit breaker HALF-OPEN for entra-metadata client — testing with the next call");
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = args =>
                    {
                        logger.LogInformation("Circuit breaker CLOSED for entra-metadata client — recovered");
                        return ValueTask.CompletedTask;
                    }
                });

                // Per-attempt timeout, innermost: bounds a single try, not the
                // whole retry sequence (the retry strategy above already
                // bounds the overall attempt count).
                resilienceBuilder.AddTimeout(options.Timeout);
            });

        return httpClientBuilder;
    }
}
