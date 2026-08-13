namespace QuotesApi.Options;

public record JwtOptions
{
    public string SigningKey { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromHours(1);
    public int RefreshTokenExpiryDays { get; init; } = 30;
}
