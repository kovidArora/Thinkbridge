using QuotesApi.Models;

namespace QuotesApi.Services;

public interface IJwtTokenService
{
    (string AccessToken, int ExpiresInSeconds) GenerateAccessToken(User user);
}