using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Repositories;

namespace QuotesApi.Commands;

public record CreateQuoteCommand(string Author, string Text, int CreatedByUserId);

public class CreateQuoteCommandHandler
{
    private readonly QuotesDbContext _db;
    private readonly IClock _clock;
    private readonly ILogger<CreateQuoteCommandHandler> _logger;

    public CreateQuoteCommandHandler(
        QuotesDbContext db,
        IClock clock,
        ILogger<CreateQuoteCommandHandler> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<(Quote? Quote, string? Error)> HandleAsync(
        CreateQuoteCommand command,
        CancellationToken cancellationToken)
    {
        var (quote, error) = Quote.Create(
            command.Author,
            command.Text,
            command.CreatedByUserId);

        if (quote is null)
        {
            return (null, error);
        }

        quote.PublishedAt = _clock.UtcNow;

        _db.Quotes.Add(quote);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created quote {QuoteId} by {Author}",
            quote.Id,
            quote.Author);

        return (quote, null);
    }
}
