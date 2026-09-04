using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.RateLimiting;
using QuotesApi.Extensions;
using QuotesApi.Options;
using QuotesApi.Services;
using System.Net;

namespace Quotes.Tests.Unit;

public class EntraMetadataClientResilienceTests
{
    private class TransientFailureThenSuccessHandler : DelegatingHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;

            if (CallCount <= 2)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"issuer\":\"https://login.microsoftonline.com/test/v2.0\"}")
            });
        }
    }

    private class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Lines { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Lines);

        public void Dispose() { }

        private class CapturingLogger : ILogger
        {
            private readonly List<string> _lines;
            public CapturingLogger(List<string> lines) => _lines = lines;

            public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                _lines.Add($"[{logLevel}] {formatter(state, exception)}");
            }
        }
    }

    [Fact]
    public async Task GetOpenIdConfigurationAsync_TransientFailuresThenSuccess_RetriesAndSucceedsWithLoggedRetries()
    {
        var loggerProvider = new CapturingLoggerProvider();
        var fakeHandler = new TransientFailureThenSuccessHandler();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(loggerProvider));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Entra:TenantId"] = "test-tenant" })
            .Build();
        services.Configure<EntraOptions>(configuration.GetSection("Entra"));

        services.AddEntraMetadataClient()
            .ConfigurePrimaryHttpMessageHandler(() => fakeHandler);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEntraMetadataClient>();

        var result = await client.GetOpenIdConfigurationAsync(CancellationToken.None);

        result.Should().Contain("issuer");
        fakeHandler.CallCount.Should().Be(3);

        var retryLogLines = loggerProvider.Lines.Where(l => l.Contains("for entra-metadata after")).ToList();
        retryLogLines.Should().HaveCount(2);
        retryLogLines[0].Should().Contain("Retry 1/3");
        retryLogLines[1].Should().Contain("Retry 2/3");

        foreach (var line in loggerProvider.Lines)
        {
            Console.WriteLine(line);
        }
    }

    [Theory]
    [InlineData("GET", true)]
    [InlineData("HEAD", true)]
    [InlineData("PUT", true)]
    [InlineData("DELETE", true)]
    [InlineData("OPTIONS", true)]
    [InlineData("POST", false)]
    [InlineData("PATCH", false)]
    public void IsIdempotent_ClassifiesHttpMethodsCorrectly(string method, bool expected)
    {
        ResilientHttpClientExtensions.IsIdempotent(new HttpMethod(method)).Should().Be(expected);
    }

    private class ControllableHandler : DelegatingHandler
    {
        public bool Fail { get; set; } = true;
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;

            if (Fail)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"issuer\":\"https://login.microsoftonline.com/test/v2.0\"}")
            });
        }
    }

    [Fact]
    public async Task GetOpenIdConfigurationAsync_SustainedFailures_OpensCircuitThenRecoversAfterBreakDuration()
    {
        var loggerProvider = new CapturingLoggerProvider();
        var fakeHandler = new ControllableHandler { Fail = true };

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(loggerProvider));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Entra:TenantId"] = "test-tenant" })
            .Build();
        services.Configure<EntraOptions>(configuration.GetSection("Entra"));

        services.AddEntraMetadataClient(new EntraResilienceOptions
            {
                MaxRetryAttempts = 1,
                RetryBackoffType = DelayBackoffType.Constant,
                RetryDelay = TimeSpan.Zero,
                CircuitBreakerFailureRatio = 0.5,
                CircuitBreakerMinimumThroughput = 2,
                CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(10),
                CircuitBreakerBreakDuration = TimeSpan.FromMilliseconds(500),
                Timeout = TimeSpan.FromSeconds(2),
            })
            .ConfigurePrimaryHttpMessageHandler(() => fakeHandler);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEntraMetadataClient>();

        // Drive sustained failures until the circuit actually opens — a
        // BrokenCircuitException is the breaker refusing the call outright,
        // distinct from the HttpRequestException a 503 response itself causes.
        var opened = false;
        for (var i = 0; i < 10 && !opened; i++)
        {
            try
            {
                await client.GetOpenIdConfigurationAsync(CancellationToken.None);
            }
            catch (BrokenCircuitException)
            {
                opened = true;
            }
            catch (HttpRequestException)
            {
                // expected failure from the 503 while the circuit is still closed
            }
        }

        opened.Should().BeTrue("sustained failures should have opened the circuit");
        loggerProvider.Lines.Should().Contain(l => l.Contains("OPENED"));

        // While open, the underlying handler should not even be called.
        var callCountAtOpen = fakeHandler.CallCount;
        await FluentActions.Awaiting(() => client.GetOpenIdConfigurationAsync(CancellationToken.None))
            .Should().ThrowAsync<BrokenCircuitException>();
        fakeHandler.CallCount.Should().Be(callCountAtOpen, "the handler must not be reached while the circuit is open");

        // Recovery: fix the dependency, wait past BreakDuration, confirm it
        // probes (half-open) then closes again.
        fakeHandler.Fail = false;
        await Task.Delay(TimeSpan.FromMilliseconds(600));

        var result = await client.GetOpenIdConfigurationAsync(CancellationToken.None);
        result.Should().Contain("issuer");
        loggerProvider.Lines.Should().Contain(l => l.Contains("HALF-OPEN"));
        loggerProvider.Lines.Should().Contain(l => l.Contains("CLOSED"));

        Console.WriteLine("=== Captured resilience log lines ===");
        foreach (var line in loggerProvider.Lines)
        {
            Console.WriteLine(line);
        }
    }

    [Fact]
    public async Task GetOpenIdConfigurationAsync_ExceedsBulkheadLimit_RejectsExcessConcurrentCalls()
    {
        var gate = new TaskCompletionSource();
        var handler = new SlowHandler(gate.Task);

        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Entra:TenantId"] = "test-tenant" })
            .Build();
        services.Configure<EntraOptions>(configuration.GetSection("Entra"));

        services.AddEntraMetadataClient(new EntraResilienceOptions
            {
                MaxRetryAttempts = 1,
                RetryBackoffType = DelayBackoffType.Constant,
                RetryDelay = TimeSpan.Zero,
                BulkheadMaxConcurrency = 2,
                BulkheadQueueLimit = 0,
                Timeout = TimeSpan.FromSeconds(5),
            })
            .ConfigurePrimaryHttpMessageHandler(() => handler);

        await using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IEntraMetadataClient>();

        // Two calls fill the bulkhead's only two permits and hang (the handler
        // won't complete until the gate is released); a third call arriving
        // while both are still in flight has nowhere to go.
        var inFlight1 = client.GetOpenIdConfigurationAsync(CancellationToken.None);
        var inFlight2 = client.GetOpenIdConfigurationAsync(CancellationToken.None);

        await FluentActions.Awaiting(() => client.GetOpenIdConfigurationAsync(CancellationToken.None))
            .Should().ThrowAsync<RateLimiterRejectedException>();

        gate.SetResult();
        await Task.WhenAll(inFlight1, inFlight2);
    }

    private class SlowHandler(Task gate) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await gate;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"issuer\":\"https://login.microsoftonline.com/test/v2.0\"}")
            };
        }
    }
}
