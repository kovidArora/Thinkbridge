using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
}
