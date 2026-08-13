using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Repositories;
using QuotesApi.Services;
using Xunit;

namespace Quotes.Tests.Unit;

public class RefreshTokenServiceTests
{
    private static QuotesDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<QuotesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new QuotesDbContext(options);
    }

    private static IConfiguration CreateConfiguration()
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:RefreshTokenExpiryDays"] = "30"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
    }

    [Fact]
    public async Task GenerateAsync_ValidUserId_CreatesTokenExpiringBasedOnClock()
    {
        var db = CreateInMemoryDb();
        var clock = Substitute.For<IClock>();
        var fixedNow = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(fixedNow);

        var sut = new RefreshTokenService(
            db,
            CreateConfiguration(),
            Substitute.For<ILogger<RefreshTokenService>>(),
            clock);

        var (rawToken, expiresAt) = await sut.GenerateAsync(userId: 1);

        rawToken.Should().NotBeNullOrEmpty();
        expiresAt.Should().Be(fixedNow.AddDays(30));

        var storedToken = await db.RefreshTokens.SingleAsync();
        storedToken.UserId.Should().Be(1);
        storedToken.Token.Should().NotBe(rawToken);
        storedToken.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAndRotateAsync_ValidToken_RevokesOldTokenAndIssuesNewOneInSameFamily()
    {
        var db = CreateInMemoryDb();
        var clock = Substitute.For<IClock>();
        var fixedNow = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(fixedNow);

        var sut = new RefreshTokenService(
            db,
            CreateConfiguration(),
            Substitute.For<ILogger<RefreshTokenService>>(),
            clock);

        var (rawToken, _) = await sut.GenerateAsync(userId: 1);

        var result = await sut.ValidateAndRotateAsync(rawToken);

        result.Succeeded.Should().BeTrue();
        result.NewRawToken.Should().NotBeNullOrEmpty();
        result.NewRawToken.Should().NotBe(rawToken);

        var allTokens = await db.RefreshTokens.ToListAsync();
        allTokens.Should().HaveCount(2);
        allTokens.Should().ContainSingle(t => t.RevokedAt != null);
        allTokens.Should().ContainSingle(t => t.RevokedAt == null);
    }

    [Fact]
    public async Task ValidateAndRotateAsync_UnknownToken_ReturnsFailureWithInvalidTokenReason()
    {
        var db = CreateInMemoryDb();

        var sut = new RefreshTokenService(
            db,
            CreateConfiguration(),
            Substitute.For<ILogger<RefreshTokenService>>(),
            Substitute.For<IClock>());

        var result = await sut.ValidateAndRotateAsync("this-token-was-never-issued");

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be("invalid_token");
    }

    [Fact]
    public async Task ValidateAndRotateAsync_ExpiredToken_ReturnsFailureWithExpiredReason()
    {
        var db = CreateInMemoryDb();
        var clock = Substitute.For<IClock>();
        var issuedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(issuedAt);

        var sut = new RefreshTokenService(
            db,
            CreateConfiguration(),
            Substitute.For<ILogger<RefreshTokenService>>(),
            clock);

        var (rawToken, _) = await sut.GenerateAsync(userId: 1);

        clock.UtcNow.Returns(issuedAt.AddDays(31));

        var result = await sut.ValidateAndRotateAsync(rawToken);

        result.Succeeded.Should().BeFalse();
        result.FailureReason.Should().Be("expired");
    }

    [Fact]
    public async Task ValidateAndRotateAsync_ReusedRevokedToken_RevokesEntireFamilyAndReturnsReuseDetected()
    {
        var db = CreateInMemoryDb();
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var sut = new RefreshTokenService(
            db,
            CreateConfiguration(),
            Substitute.For<ILogger<RefreshTokenService>>(),
            clock);

        var (originalToken, _) = await sut.GenerateAsync(userId: 1);
        var firstRotation = await sut.ValidateAndRotateAsync(originalToken);
        firstRotation.Succeeded.Should().BeTrue();

        var reuseResult = await sut.ValidateAndRotateAsync(originalToken);

        reuseResult.Succeeded.Should().BeFalse();
        reuseResult.FailureReason.Should().Be("reuse_detected");

        var allTokens = await db.RefreshTokens.ToListAsync();
        allTokens.Should().OnlyContain(t => t.RevokedAt != null);
    }

    [Fact]
    public async Task RevokeAsync_ValidToken_SetsRevokedAtUsingClock()
    {
        var db = CreateInMemoryDb();
        var clock = Substitute.For<IClock>();
        var fixedNow = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(fixedNow);

        var sut = new RefreshTokenService(
            db,
            CreateConfiguration(),
            Substitute.For<ILogger<RefreshTokenService>>(),
            clock);

        var (rawToken, _) = await sut.GenerateAsync(userId: 1);

        await sut.RevokeAsync(rawToken);

        var storedToken = await db.RefreshTokens.SingleAsync();
        storedToken.RevokedAt.Should().Be(fixedNow);
    }

    [Fact]
    public async Task RevokeAsync_UnknownToken_DoesNotThrow()
    {
        var db = CreateInMemoryDb();

        var sut = new RefreshTokenService(
            db,
            CreateConfiguration(),
            Substitute.For<ILogger<RefreshTokenService>>(),
            Substitute.For<IClock>());

        var act = async () => await sut.RevokeAsync("never-issued-token");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RevokeAsync_AlreadyRevokedToken_IsIdempotentAndDoesNotChangeRevokedAt()
    {
        var db = CreateInMemoryDb();
        var clock = Substitute.For<IClock>();
        var firstRevokeTime = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(firstRevokeTime);

        var sut = new RefreshTokenService(
            db,
            CreateConfiguration(),
            Substitute.For<ILogger<RefreshTokenService>>(),
            clock);

        var (rawToken, _) = await sut.GenerateAsync(userId: 1);
        await sut.RevokeAsync(rawToken);

        clock.UtcNow.Returns(firstRevokeTime.AddDays(1));
        await sut.RevokeAsync(rawToken);

        var storedToken = await db.RefreshTokens.SingleAsync();
        storedToken.RevokedAt.Should().Be(firstRevokeTime);
    }
}
