namespace QuotesApi.Models;
//gives the shape of the database
public class Quote
{
    public int Id { get; set; }

    public string Author { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}