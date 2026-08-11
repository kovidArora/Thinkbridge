using FluentAssertions;
using QuotesApi.Models;
using Xunit;

namespace Tests.Domain;

public class CollectionTests
{
    [Fact]
    public void EmptyName_Throws()
    {
        var act = () => new Collection("", ownerId: 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NameOver80Chars_Throws()
    {
        var longName = new string('a', 81);
        var act = () => new Collection(longName, ownerId: 1);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Adding51stItem_Throws()
    {
        var collection = new Collection("Test Collection", ownerId: 1);
        for (int i = 1; i <= 50; i++)
            collection.AddItem(i);

        var act = () => collection.AddItem(51);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddingDuplicateQuoteId_Throws()
    {
        var collection = new Collection("Test Collection", ownerId: 1);
        collection.AddItem(1);

        var act = () => collection.AddItem(1);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RemovingNonExistentItem_Throws()
    {
        var collection = new Collection("Test Collection", ownerId: 1);

        var act = () => collection.RemoveItem(99);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AddThenRemove_LeavesZeroItems()
    {
        var collection = new Collection("Test Collection", ownerId: 1);
        collection.AddItem(1);
        collection.RemoveItem(1);

        collection.Items.Should().BeEmpty();
    }
}