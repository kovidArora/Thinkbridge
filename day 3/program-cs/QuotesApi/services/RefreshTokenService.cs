using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;
using System.Security.Cryptography;
using System.Text;
using QuotesApi.Repositories;
namespace QuotesApi.Services;
 
public class RefreshTokenService : IRefreshTokenService
{
    private readonly QuotesDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RefreshTokenService> _logger;
    private readonly IClock _clock;
 
    public RefreshTokenService(
        QuotesDbContext db,
        IConfiguration configuration,
        ILogger<RefreshTokenService> logger,
        IClock clock)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
        _clock = clock;
    }
 
    private int RefreshTokenExpiryDays =>
        int.TryParse(_configuration["Jwt:RefreshTokenExpiryDays"], out var days) ? days : 30;
 
    public async Task<(string RawToken, DateTimeOffset ExpiresAt)> GenerateAsync(
        int userId,
        string? familyId = null)
    {
        var rawToken = GenerateRawToken();
        var expiresAt = _clock.UtcNow.AddDays(RefreshTokenExpiryDays);
 
        var entity = new RefreshToken
        {
            Token = Hash(rawToken),
            UserId = userId,
            ExpiresAt = expiresAt,
            FamilyId = familyId ?? Guid.NewGuid().ToString("N")
        };
 
        _db.RefreshTokens.Add(entity);
        await _db.SaveChangesAsync();
 
        return (rawToken, expiresAt);
    }
 
    public async Task<RefreshResult> ValidateAndRotateAsync(string rawToken)
    {
        var hashed = Hash(rawToken);
 
        var existing = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == hashed);
 
        if (existing is null)
        {
            return new RefreshResult(false, null, null, null, "invalid_token");
        }
 
        var now = _clock.UtcNow;
 
        if (existing.RevokedAt is not null)
        {
            // This exact token was already rotated away or revoked.
            // Someone is replaying an old refresh token -> treat the
            // whole family as compromised.
            await RevokeFamilyAsync(existing.FamilyId, now);
 
            _logger.LogWarning(
                "Refresh token reuse detected. UserId={UserId} FamilyId={FamilyId} TokenId={TokenId}",
                existing.UserId,
                existing.FamilyId,
                existing.Id);
 
            return new RefreshResult(false, existing.UserId, null, null, "reuse_detected");
        }
 
        if (existing.ExpiresAt < now)
        {
            return new RefreshResult(false, existing.UserId, null, null, "expired");
        }
 
        var newRawToken = GenerateRawToken();
        var newHashed = Hash(newRawToken);
        var newExpiresAt = now.AddDays(RefreshTokenExpiryDays);
 
        existing.RevokedAt = now;
        existing.ReplacedByToken = newHashed;
 
        _db.RefreshTokens.Add(new RefreshToken
        {
            Token = newHashed,
            UserId = existing.UserId,
            ExpiresAt = newExpiresAt,
            FamilyId = existing.FamilyId
        });
 
        await _db.SaveChangesAsync();
 
        return new RefreshResult(true, existing.UserId, newRawToken, newExpiresAt, null);
    }
 
    public async Task RevokeAsync(string rawToken)
    {
        var hashed = Hash(rawToken);
 
        var existing = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == hashed);
 
        // Idempotent: unknown/already-revoked tokens are a no-op,
        // and we don't leak which case it was.
        if (existing is null || existing.RevokedAt is not null)
        {
            return;
        }
 
        existing.RevokedAt = _clock.UtcNow;
        await _db.SaveChangesAsync();
    }
 
    private async Task RevokeFamilyAsync(string familyId, DateTimeOffset now)
    {
        var tokens = await _db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync();
 
        foreach (var token in tokens)
        {
            token.RevokedAt = now;
        }
 
        await _db.SaveChangesAsync();
    }
 
    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }
 
    private static string Hash(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
 
