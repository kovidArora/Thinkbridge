namespace QuotesApi.Models;

public class RefreshToken
{
    public int Id { get; set; }

    public string Token { get; set; } = string.Empty;

    public int UserId { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? ReplacedByToken { get; set; }

    // Used to identify all tokens belonging to the same rotation chain.
    public string FamilyId { get; set; } = string.Empty;
}