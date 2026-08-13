using FluentAssertions;
using QuotesApi.Models;
using QuotesApi.Options;
using QuotesApi.Services;
using System.IdentityModel.Tokens.Jwt;
using Xunit;
using System.Security.Claims;

namespace Quotes.Tests.Unit;

public class JwtTokenServiceTests
{
    private static TestOptionsSnapshot<JwtOptions> CreateOptions()
    {
        return new TestOptionsSnapshot<JwtOptions>(new JwtOptions
        {
            SigningKey = "this-is-a-32-byte-secret-key-1234"
        });
    }

    [Fact]
    public void GenerateAccessToken_ValidUser_ReturnsTokenWithExpectedClaims()
    {
        var sut = new JwtTokenService(CreateOptions());
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
        var sut = new JwtTokenService(CreateOptions());
        var user = new User { Id = 1, Email = "test@example.com" };

        var (accessToken, _) = sut.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(accessToken);

        jwt.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddHours(1), TimeSpan.FromSeconds(5));
    }
}
