using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using QuotesApi.Data;
using QuotesApi.Models;
using System.Text.Json.Serialization;
using Xunit;

namespace Quotes.Tests.Integration;

public class QuoteEndpointTests
{
    [Fact]
    public async Task Post_Quote_AuthenticatedWithValidBody_Returns201Created()
    {
        using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var token = await LoginAsync(factory, client, "happypath@example.com");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/quotes", new
        {
            author = "Maya Angelou",
            text = "There is no greater agony than bearing an untold story inside you."
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<QuoteResponse>();
        Assert.NotNull(created);
        Assert.Equal("Maya Angelou", created!.Author);
    }

    [Fact]
    public async Task Get_QuotesWithZeroPage_Returns400ValidationProblem()
    {
        using var factory = new TestWebApplicationFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/quotes?page=0&size=10");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemResponse>();
        Assert.NotNull(problem);
        Assert.True(problem!.Errors.ContainsKey("page"));
    }

    private async Task<string> LoginAsync(TestWebApplicationFactory factory, HttpClient client, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();

        db.Users.Add(new User
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!")
        });
        await db.SaveChangesAsync();

        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "Password123!"
        });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.AccessToken;
    }

    private record LoginResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private record QuoteResponse(int Id, string Author, string Text);

    private record ValidationProblemResponse(
        [property: JsonPropertyName("errors")] Dictionary<string, string[]> Errors);
}
