namespace QuotesApi.Services;

public record RefreshResult(
    bool Succeeded,
    int? UserId,
    string? NewRawToken,
    DateTimeOffset? NewExpiresAt,
    string? FailureReason);

public interface IRefreshTokenService
{
    Task<(string RawToken, DateTimeOffset ExpiresAt)> GenerateAsync(int userId, string? familyId = null);
    Task<RefreshResult> ValidateAndRotateAsync(string rawToken);
    Task RevokeAsync(string rawToken);
}