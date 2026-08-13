using FluentAssertions;
using QuotesApi.Models;
using Xunit;

namespace Quotes.Tests.Unit;

public class CollectionTests
{
    [Fact]
    public void Constructor_ValidNameAndOwnerId_CreatesCollection()
    {
        var collection = new Collection("My Favorites", ownerId: 1);

        collection.Name.Should().Be("My Favorites");
        collection.OwnerId.Should().Be(1);
        collection.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_EmptyOrWhitespaceName_ThrowsArgumentException(string? name)
    {
        var act = () => new Collection(name!, ownerId: 1);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Collection name must be between 3 and 80 characters.");
    }

    [Theory]
    [InlineData("a")]
    [InlineData("ab")]
    public void Constructor_NameShorterThan3Characters_ThrowsArgumentException(string name)
    {
        var act = () => new Collection(name, ownerId: 1);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Collection name must be between 3 and 80 characters.");
    }

    [Fact]
    public void Constructor_NameExceeding80Characters_ThrowsArgumentException()
    {
        var tooLongName = new string('a', 81);

        var act = () => new Collection(tooLongName, ownerId: 1);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Collection name must be between 3 and 80 characters.");
    }

    [Fact]
    public void Constructor_NameExactly3Characters_CreatesCollection()
    {
        var collection = new Collection("abc", ownerId: 1);

        collection.Name.Should().Be("abc");
    }

    [Fact]
    public void Constructor_NameExactly80Characters_CreatesCollection()
    {
        var exactLengthName = new string('a', 80);

        var collection = new Collection(exactLengthName, ownerId: 1);

        collection.Name.Should().Be(exactLengthName);
    }

    [Fact]
    public void AddItem_NewQuoteId_AddsItemToCollection()
    {
        var collection = new Collection("Favorites", ownerId: 1);

        collection.AddItem(quoteId: 100);

        collection.Items.Should().ContainSingle(i => i.QuoteId == 100);
    }

    [Fact]
    public void AddItem_DuplicateQuoteId_ThrowsInvalidOperationException()
    {
        var collection = new Collection("Favorites", ownerId: 1);
        collection.AddItem(quoteId: 100);

        var act = () => collection.AddItem(quoteId: 100);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("This quote is already in the collection.");
    }

    [Fact]
    public void AddItem_CollectionAt50Items_ThrowsInvalidOperationException()
    {
        var collection = new Collection("Favorites", ownerId: 1);
        for (var i = 0; i < 50; i++)
        {
            collection.AddItem(quoteId: i);
        }

        var act = () => collection.AddItem(quoteId: 999);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("A collection cannot contain more than 50 quotes.");
    }

    [Fact]
    public void RemoveItem_ExistingQuoteId_RemovesItemFromCollection()
    {
        var collection = new Collection("Favorites", ownerId: 1);
        collection.AddItem(quoteId: 100);

        collection.RemoveItem(quoteId: 100);

        collection.Items.Should().BeEmpty();
    }

    [Fact]
    public void RemoveItem_NonExistentQuoteId_ThrowsInvalidOperationException()
    {
        var collection = new Collection("Favorites", ownerId: 1);

        var act = () => collection.RemoveItem(quoteId: 999);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("This quote is not in the collection.");
    }
}
