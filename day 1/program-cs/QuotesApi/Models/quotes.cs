namespace QuotesApi.Models;

public class Quote
{
    public int Id { get; private set; }
    public string Author { get; private set; } = string.Empty;
    public string Text { get; private set; } = string.Empty;
    public DateTimeOffset PublishedAt { get; set; }
    public bool IsDeleted { get; private set; }

    private Quote() { } // EF Core

    private Quote(string author, string text)
    {
        Author = author;
        Text = text;
    }

    public static (Quote? Quote, string? Error) Create(string author, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 1000)
        {
            return (null, "Text must be between 1 and 1000 characters.");
        }

        if (string.IsNullOrWhiteSpace(author) || author.Length > 200)
        {
            return (null, "Author must be between 1 and 200 characters.");
        }

        return (new Quote(author, text), null);
    }

    public void Delete()
    {
        IsDeleted = true;
    }
}