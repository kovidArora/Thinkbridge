using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using QuotesApi.Models;
using QuotesApi.Repositories;
using Xunit;
using System.Net.Http.Json;

namespace QuotesApi.Tests;

public class CollectionCancellationIntegrationTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CollectionCancellationIntegrationTests(
        WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateCollection_Cancels_WhenRequestIsCancelled()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddScoped<ICollectionRepository, SlowCollectionRepository>();
            });
        });

        var client = factory.CreateClient();

        using var cts = new CancellationTokenSource();

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/collections")
        {
            Content = JsonContent.Create(new
            {
                name = "Test Collection",
                ownerId = 1
            })
        };

        var requestTask = client.SendAsync(request, cts.Token);

        await Task.Delay(100);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => requestTask);
    }

    private class SlowCollectionRepository : ICollectionRepository
    {
        public async Task Add(
            Collection collection,
            CancellationToken cancellationToken)
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);
        }

        public Task<Collection?> GetById(
            int id,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<Collection?>(null);
        }

        public Task Update(
            Collection collection,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task Delete(
            Collection collection,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}