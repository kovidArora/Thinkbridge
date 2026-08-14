using FluentAssertions;
using Microsoft.Extensions.Configuration;
using QuotesApi.Models;
using QuotesApi.Services;
using System.IdentityModel.Tokens.Jwt;
using Xunit;
using System.Security.Claims;

namespace Quotes.Tests.Unit;

public class JwtTokenServiceTests
{
    private static IConfiguration CreateConfiguration()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "this-is-a-32-byte-secret-key-1234"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    [Fact]
    public void GenerateAccessToken_ValidUser_ReturnsTokenWithExpectedClaims()
    {
        var sut = new JwtTokenService(CreateConfiguration());
        var user = new User { Id = 42, Email = "test@example.com" };

        var (accessToken, expiresInSeconds) = sut.GenerateAccessToken(user);

        accessToken.Should().NotBeNullOrEmpty();
        expiresInSeconds.Should().Be(3600);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(accessToken);

        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == "42");
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Email && c.Value == "test@example.com");
        jwt.Claims.Should().Contain(c => c.Type == "scope" && c.Value == "quotes.write");
    }

    [Fact]
    public void GenerateAccessToken_ValidUser_SetsExpiryApproximatelyOneHourFromNow()
    {
        var sut = new JwtTokenService(CreateConfiguration());
        var user = new User { Id = 1, Email = "test@example.com" };

        var (accessToken, _) = sut.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(accessToken);

        jwt.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddHours(1), TimeSpan.FromSeconds(5));
    }
}
