using FluentAssertions;
using QuotesApi.Models;
using Xunit;

namespace Quotes.Tests.Unit;

public class QuoteTests
{
    [Fact]
    public void Create_ValidAuthorAndText_ReturnsQuoteWithNoError()
    {
        var (quote, error) = Quote.Create("Mark Twain", "The secret of getting ahead is getting started.", 1);

        quote.Should().NotBeNull();
        error.Should().BeNull();
        quote!.Author.Should().Be("Mark Twain");
        quote.Text.Should().Be("The secret of getting ahead is getting started.");
        quote.CreatedByUserId.Should().Be(1);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_EmptyOrWhitespaceText_ReturnsNullQuoteAndError(string? text)
    {
        var (quote, error) = Quote.Create("Author", text!, 1);

        quote.Should().BeNull();
        error.Should().Be("Text must be between 1 and 1000 characters.");
    }

    [Fact]
    public void Create_TextExceeding1000Characters_ReturnsNullQuoteAndError()
    {
        var tooLongText = new string('a', 1001);

        var (quote, error) = Quote.Create("Author", tooLongText, 1);

        quote.Should().BeNull();
        error.Should().Be("Text must be between 1 and 1000 characters.");
    }

    [Fact]
    public void Create_TextExactly1000Characters_ReturnsQuoteWithNoError()
    {
        var exactLengthText = new string('a', 1000);

        var (quote, error) = Quote.Create("Author", exactLengthText, 1);

        quote.Should().NotBeNull();
        error.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_EmptyOrWhitespaceAuthor_ReturnsNullQuoteAndError(string? author)
    {
        var (quote, error) = Quote.Create(author!, "Some valid text", 1);

        quote.Should().BeNull();
        error.Should().Be("Author must be between 1 and 200 characters.");
    }

    [Fact]
    public void Create_AuthorExceeding200Characters_ReturnsNullQuoteAndError()
    {
        var tooLongAuthor = new string('a', 201);

        var (quote, error) = Quote.Create(tooLongAuthor, "Some valid text", 1);

        quote.Should().BeNull();
        error.Should().Be("Author must be between 1 and 200 characters.");
    }

    [Fact]
    public void Create_AuthorExactly200Characters_ReturnsQuoteWithNoError()
    {
        var exactLengthAuthor = new string('a', 200);

        var (quote, error) = Quote.Create(exactLengthAuthor, "Some valid text", 1);

        quote.Should().NotBeNull();
        error.Should().BeNull();
    }

    [Fact]
    public void Delete_ValidQuote_SetsIsDeletedTrue()
    {
        var (quote, _) = Quote.Create("Author", "Text", 1);

        quote!.Delete();

        quote.IsDeleted.Should().BeTrue();
    }
}
