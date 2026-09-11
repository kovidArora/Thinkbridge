using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Tokens;
using QuotesApi;
using QuotesApi.Commands;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Queries;
using QuotesApi.Repositories;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
 
namespace QuotesApi.Extensions;
 
public static class QuoteEndpointExtensions
{
    // Applies to every paginated list endpoint below — without an upper
    // bound, a single caller could request an unbounded result set (e.g.
    // size=999999999) and force the backend to materialize and serialize
    // far more data than any real page needs.
    private const int MaxPageSize = 100;

    private static string[]? ValidatePaging(int page, int size)
    {
        if (page < 1)
        {
            return ["Page must be greater than 0."];
        }

        if (size < 1 || size > MaxPageSize)
        {
            return [$"Size must be between 1 and {MaxPageSize}."];
        }

        return null;
    }

    public static void MapQuoteEndpoints(this WebApplication app)
    {


        app.MapGet("/api/quotes", async (
            int page,
            int size,
            string? author,
            IQuoteRepository repository,
            CancellationToken cancellationToken) =>
        {
            if (ValidatePaging(page, size) is { } pagingErrors)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [size < 1 || size > MaxPageSize ? "size" : "page"] = pagingErrors
                });
            }

            var quotes = await repository.GetQuotesAsync(
                page,
                size,
                author,
                cancellationToken);

            return Results.Ok(quotes);
        });

        // Exposes author email addresses (see QuoteReadModel) — read access
        // alone is a real information-disclosure surface, not something to
        // leave open to anonymous callers the way the plain quotes list is.
        app.MapGet("/api/quotes/with-authors", async (
            int page,
            int size,
            QuoteReadModel readModel,
            CancellationToken cancellationToken) =>
        {
            if (ValidatePaging(page, size) is { } pagingErrors)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [size < 1 || size > MaxPageSize ? "size" : "page"] = pagingErrors
                });
            }

            var quotes = await readModel.GetQuotesWithAuthorEmailAsync(
                page,
                size,
                cancellationToken);

            return Results.Ok(quotes);
        })
        .RequireAuthorization();

        app.MapGet("/api/authors/stats", async (
            IQuoteRepository repository,
            HybridCache cache,
            bool? noCache,
            CancellationToken cancellationToken) =>
        {
            // noCache exists only to get an honest "without caching" baseline
            // for load testing against the exact same endpoint/code path.
            var stats = noCache == true
                ? await repository.GetAuthorStatsAsync(cancellationToken)
                : await cache.GetOrCreateAsync(
                    "authors:stats",
                    async ct => await repository.GetAuthorStatsAsync(ct),
                    cancellationToken: cancellationToken);

            return Results.Ok(stats);
        });

        // Both debug endpoints were previously callable by anyone, logged in
        // or not — the evict one is a free, unauthenticated way to force a
        // cache miss (and the resulting DB query) on demand, a crude but
        // real denial-of-service lever against the caching layer.
        app.MapGet("/api/debug/author-stats-metrics", (AuthorStatsQueryMetrics metrics) =>
            Results.Ok(new { dbQueryCount = metrics.DbQueryCount }))
        .RequireAuthorization();

        // Load-test-only: force a cold cache key on demand instead of waiting
        // out the real TTL, so a stampede test starts from a known state.
        app.MapPost("/api/debug/author-stats-cache/evict", async (HybridCache cache, CancellationToken cancellationToken) =>
        {
            await cache.RemoveAsync("authors:stats", cancellationToken);
            return Results.NoContent();
        })
        .RequireAuthorization();

        app.MapGet("/api/quotes/{id:int}", async (
            int id,
            IQuoteRepository repository,
            CancellationToken cancellationToken) =>
        {
            var quote = await repository.GetByIdAsync(
                id,
                cancellationToken);
 
            return quote is null
                ? Results.NotFound()
                : Results.Ok(quote);
        });
 
        app.MapPost("/api/quotes", async (
            CreateQuoteRequest request,
            CreateQuoteCommandHandler commandHandler,
            ClaimsPrincipal user,
            HybridCache cache,
            CancellationToken cancellationToken) =>
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var (quote, error) = await commandHandler.HandleAsync(
                new CreateQuoteCommand(request.Author, request.Text, userId),
                cancellationToken);
            if (quote is null)
            {
                var field = error!.StartsWith("Text", StringComparison.Ordinal) ? "text" : "author";

                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [field] = [error]
                });
            }

            // Author quote counts changed — without this, the cached
            // "authors:stats" response would keep serving the pre-creation
            // count for up to its full 30s TTL.
            await cache.RemoveAsync("authors:stats", cancellationToken);

            return Results.Created(
                $"/api/quotes/{quote.Id}",
                quote);
        })
        .RequireAuthorization("can-edit-quotes");

        app.MapDelete("/api/quotes/{id:int}", async (
            int id,
            IQuoteRepository repository,
            HybridCache cache,
            CancellationToken cancellationToken) =>
        {
            var deleted = await repository.DeleteAsync(
                id,
                cancellationToken);

            if (deleted)
            {
                // Same reasoning as the create path above — a deletion also
                // changes an author's count.
                await cache.RemoveAsync("authors:stats", cancellationToken);
            }

            return deleted
                ? Results.NoContent()
                : Results.NotFound();
        })
        .RequireAuthorization("must-own-quote");
 
 
        app.MapPost("/api/collections", async (
            CreateCollectionRequest request,
            ICollectionRepository repository,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var collection = new Collection(
                    request.Name,
                    request.OwnerId);
 
                await repository.Add(
                    collection,
                    cancellationToken);
 
                return Results.Created(
                    $"/api/collections/{collection.Id}",
                    collection);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        })
        .RequireAuthorization();

        app.MapPost(
            "/api/collections/{id:int}/items/{quoteId:int}",
            async (
                int id,
                int quoteId,
                ICollectionRepository repository,
                CancellationToken cancellationToken) =>
            {
                var collection = await repository.GetById(
                    id,
                    cancellationToken);
 
                if (collection is null)
                {
                    return Results.NotFound();
                }
 
                try
                {
                    collection.AddItem(quoteId);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Problem(
                        statusCode: 400,
                        title: "Collection invariant violated",
                        detail: ex.Message);
                }
 
                await repository.Update(
                    collection,
                    cancellationToken);

                return Results.Ok(collection);
            })
            .RequireAuthorization();

        app.MapDelete(
            "/api/collections/{id:int}/items/{quoteId:int}",
            async (
                int id,
                int quoteId,
                ICollectionRepository repository,
                CancellationToken cancellationToken) =>
            {
                var collection = await repository.GetById(
                    id,
                    cancellationToken);
 
                if (collection is null)
                {
                    return Results.NotFound();
                }
 
                try
                {
                    collection.RemoveItem(quoteId);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Problem(
                        statusCode: 400,
                        title: "Collection invariant violated",
                        detail: ex.Message);
                }
 
                await repository.Update(
                    collection,
                    cancellationToken);

                return Results.Ok(collection);
            })
            .RequireAuthorization();
    }
}
 
public record CreateQuoteRequest(
    string Author,
    string Text);
 
public record CreateCollectionRequest(
    string Name,
    int OwnerId);
