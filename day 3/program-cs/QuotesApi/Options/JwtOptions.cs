namespace QuotesApi.Options;

public record JwtOptions
{
    public string Key { get; init; } = string.Empty;
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromHours(1);
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(30);
}
