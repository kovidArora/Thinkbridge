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
    }
}

public record CreateQuoteRequest(
    string Author,
    string Text);