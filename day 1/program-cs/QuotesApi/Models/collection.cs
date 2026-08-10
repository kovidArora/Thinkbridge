namespace QuotesApi.Models;

public class Collection
{
    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public int OwnerId { get; private set; }

    public List<CollectionItem> Items { get; private set; } = new();

    private Collection() { } // EF

    public Collection(string name, int ownerId)
    {
        ValidateName(name);

        Name = name;
        OwnerId = ownerId;
    }

    public void AddItem(int quoteId)
    {
        if (Items.Count >= 50)
            throw new InvalidOperationException(
                "A collection cannot contain more than 50 quotes.");

        if (Items.Any(x => x.QuoteId == quoteId))
            throw new InvalidOperationException(
                "This quote is already in the collection.");

        Items.Add(new CollectionItem(quoteId));
    }

    public void RemoveItem(int quoteId)
    {
        var item = Items.FirstOrDefault(x => x.QuoteId == quoteId);

        if (item == null)
            throw new InvalidOperationException(
                "This quote is not in the collection.");

        Items.Remove(item);
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            name.Length < 3 ||
            name.Length > 80)
        {
            throw new ArgumentException(
                "Collection name must be between 3 and 80 characters.");
        }
    }
}