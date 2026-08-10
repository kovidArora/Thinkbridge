using Microsoft.AspNetCore.Mvc;
using QuotesApi.Models;
using QuotesApi.Repositories;

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
            CancellationToken cancellationToken) =>
        {
            var errors = new Dictionary<string, string[]>();

            if (string.IsNullOrWhiteSpace(request.Author))
            {
                errors["author"] = ["Author is required."];
            }

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                errors["text"] = ["Text is required."];
            }

            if (errors.Count > 0)
            {
                return Results.ValidationProblem(errors);
            }

            var quote = new Quote
            {
                Author = request.Author,
                Text = request.Text
            };

            var created = await repository.AddAsync(
                quote,
                cancellationToken);

            return Results.Created(
                $"/api/quotes/{created.Id}",
                created);
        });

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
        });


        // =========================
        // Collection endpoints
        // =========================

        app.MapPost("/api/collections", async (
            CreateCollectionRequest request,
            ICollectionRepository repository) =>
        {
            try
            {
                var collection = new Collection(
                    request.Name,
                    request.OwnerId);

                await repository.Add(collection);

                return Results.Created(
                    $"/api/collections/{collection.Id}",
                    collection);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(ex.Message);
            }
        });

        app.MapPost("/api/collections/{id:int}/items/{quoteId:int}", async (
            int id,
            int quoteId,
            ICollectionRepository repository) =>
        {
            var collection = await repository.GetById(id);

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
            await repository.Update(collection);

            return Results.Ok(collection);
        });

        app.MapDelete("/api/collections/{id:int}/items/{quoteId:int}", async (
            int id,
            int quoteId,
            ICollectionRepository repository) =>
        {
            var collection = await repository.GetById(id);

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

            await repository.Update(collection);

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