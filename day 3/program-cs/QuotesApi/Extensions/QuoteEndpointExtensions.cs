using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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
    public static void MapQuoteEndpoints(this WebApplication app)
    {

 
        app.MapGet("/api/quotes", async (
            int page,
            int size,
            IQuoteRepository repository,
            CancellationToken cancellationToken) =>
        {
            if (page < 1)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["page"] = ["Page must be greater than 0."]
                });
            }
 
            if (size < 1)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["size"] = ["Size must be greater than 0."]
                });
            }
 
            var quotes = await repository.GetQuotesAsync(
                page,
                size,
                cancellationToken);
 
            return Results.Ok(quotes);
        });
 
        app.MapGet("/api/quotes/with-authors", async (
            int page,
            int size,
            QuoteReadModel readModel,
            CancellationToken cancellationToken) =>
        {
            var quotes = await readModel.GetQuotesWithAuthorEmailAsync(
                page,
                size,
                cancellationToken);

            return Results.Ok(quotes);
        });

        app.MapGet("/api/authors/stats", async (
            IQuoteRepository repository,
            CancellationToken cancellationToken) =>
        {
            var stats = await repository.GetAuthorStatsAsync(cancellationToken);

            return Results.Ok(stats);
        });

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
                return Results.BadRequest(error);
            }

            return Results.Created(
                $"/api/quotes/{quote.Id}",
                quote);
        })
        .RequireAuthorization("can-edit-quotes");
 
        app.MapDelete("/api/quotes/{id:int}", async (
            int id,
            IQuoteRepository repository,
            CancellationToken cancellationToken) =>
        {
            var deleted = await repository.DeleteAsync(
                id,
                cancellationToken);
 
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
        });
 
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
            });
 
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
            });
    }
}
 
public record CreateQuoteRequest(
    string Author,
    string Text);
 
public record CreateCollectionRequest(
    string Name,
    int OwnerId);
