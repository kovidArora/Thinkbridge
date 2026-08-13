using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Models;
using QuotesApi.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace QuotesApi.Services;

public class JwtTokenService : IJwtTokenService
{
    private readonly IOptionsSnapshot<JwtOptions> _options;

    public JwtTokenService(IOptionsSnapshot<JwtOptions> options)
    {
        _options = options;
    }

    public (string AccessToken, int ExpiresInSeconds) GenerateAccessToken(User user)
    {
        var jwtOptions = _options.Value;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("scope", "quotes.write")
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var expiresInSeconds = (int)jwtOptions.AccessTokenLifetime.TotalSeconds;
        var expires = DateTime.UtcNow.Add(jwtOptions.AccessTokenLifetime);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return (accessToken, expiresInSeconds);
    }
}