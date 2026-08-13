using QuotesApi.Models;
using QuotesApi.Repositories;
using Xunit;

namespace QuotesApi.Tests;

public class CollectionCancellationTests
{
    [Fact]
    public async Task AddCollection_Cancels_WhenTokenIsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var repository = new FakeCollectionRepository();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => repository.Add(
                new Collection("Test", 1),
                cts.Token));
    }

    private class FakeCollectionRepository : ICollectionRepository
    {
        public Task<Collection?> GetById(
            int id,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<Collection?>(null);
        }

        public Task Add(
            Collection collection,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task Update(
            Collection collection,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public Task Delete(
            Collection collection,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}