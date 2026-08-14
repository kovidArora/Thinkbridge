namespace QuotesApi.Options;

public record EntraOptions
{
    public string TenantId { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
}
