using Microsoft.Extensions.Http.Resilience;
using Polly;
using QuotesApi.Services;

namespace QuotesApi.Extensions;

public static class ResilientHttpClientExtensions
{
    public static IHttpClientBuilder AddEntraMetadataClient(this IServiceCollection services)
    {
        var httpClientBuilder = services.AddHttpClient<IEntraMetadataClient, EntraMetadataClient>("entra-metadata");

        httpClientBuilder.AddResilienceHandler("default", (resilienceBuilder, context) =>
            {
                var logger = context.ServiceProvider.GetRequiredService<ILogger<EntraMetadataClient>>();

                resilienceBuilder.AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = 3,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    OnRetry = args =>
                    {
                        logger.LogWarning(
                            "Retry {AttemptNumber}/{MaxAttempts} for entra-metadata after {Delay}ms, reason: {Reason}",
                            args.AttemptNumber + 1,
                            3,
                            args.RetryDelay.TotalMilliseconds,
                            args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
                        return ValueTask.CompletedTask;
                    }
                });

                resilienceBuilder.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = 0.5,
                    SamplingDuration = TimeSpan.FromSeconds(30),
                    MinimumThroughput = 4,
                    BreakDuration = TimeSpan.FromSeconds(30),
                    OnOpened = args =>
                    {
                        logger.LogError("Circuit breaker opened for entra-metadata client");
                        return ValueTask.CompletedTask;
                    }
                });

                resilienceBuilder.AddTimeout(TimeSpan.FromSeconds(10));
            });

        return httpClientBuilder;
    }
}
