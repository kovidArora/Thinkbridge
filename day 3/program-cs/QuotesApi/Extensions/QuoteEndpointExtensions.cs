using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Data;
using QuotesApi.Models;
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
        // =========================
        // Quote endpoints
        // =========================
 
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
            IQuoteRepository repository,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
 
            if (!int.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }
 
            var (quote, error) = Quote.Create(
                request.Author,
                request.Text,
                userId);
 
            if (quote is null)
            {
                return Results.BadRequest(error);
            }
 
            var created = await repository.AddAsync(
                quote,
                cancellationToken);
 
            return Results.Created(
                $"/api/quotes/{created.Id}",
                created);
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
 
        // =========================
        // Collection endpoints
        // =========================
 
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
