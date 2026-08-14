using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using QuotesApi.Data;
using QuotesApi.Models;
using Xunit;
using System.Text.Json.Serialization;

namespace QuotesApi.Tests.Integration;

public class AuthIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Post_Quote_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/quotes", new
        {
            author = "Test",
            text = "Should fail anonymously"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Quote_WrongOwner_Returns403()
    {
        var client = _factory.CreateClient();

        var tokenA = await LoginAsync(client, "usera@example.com");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenA);

        var createResponse = await client.PostAsJsonAsync("/api/quotes", new
        {
            author = "User A",
            text = "Owned by user A"
        });
        createResponse.EnsureSuccessStatusCode();

        var created = await createResponse.Content.ReadFromJsonAsync<QuoteResponse>();

        var tokenB = await LoginAsync(client, "userb@example.com");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenB);

        var deleteResponse = await client.DeleteAsync($"/api/quotes/{created!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Post_Quote_AuthenticatedWithCorrectPolicy_Returns201()
    {
        var client = _factory.CreateClient();

        var token = await LoginAsync(client, "validuser@example.com");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/quotes", new
        {
            author = "Valid Author",
            text = "Should succeed"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Post_Quote_ExpiredToken_Returns401()
    {
        var client = _factory.CreateClient();

        var expiredToken = TestTokenFactory.CreateExpiredToken();

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await client.PostAsJsonAsync("/api/quotes", new
        {
            author = "Test",
            text = "Should fail, token expired"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
   public async Task Refresh_WithRevokedToken_Returns401()
    {
        var client = _factory.CreateClient();

      
        await LoginAsync(client, "revokeuser@example.com");

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "revokeuser@example.com",
            password = "Password123!"
        });
        loginResponse.EnsureSuccessStatusCode();

        var login = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var firstRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = login!.RefreshToken
        });
        firstRefresh.EnsureSuccessStatusCode();

        var reuseAttempt = await client.PostAsJsonAsync("/api/auth/refresh", new
        {
            refreshToken = login.RefreshToken
        });

        Assert.Equal(HttpStatusCode.Unauthorized, reuseAttempt.StatusCode);
    }
    private async Task<string> LoginAsync(HttpClient client, string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();

        if (!db.Users.Any(u => u.Email == email))
        {
            db.Users.Add(new User
            {
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!")
            });
            await db.SaveChangesAsync();
        }

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
}
