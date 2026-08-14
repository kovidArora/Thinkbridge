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
    private readonly IOptionsSnapshot<JwtOptions> _jwtOptions;

    public JwtTokenService(IOptionsSnapshot<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions;
    }

    public (string AccessToken, int ExpiresInSeconds) GenerateAccessToken(User user)
    {
        var options = _jwtOptions.Value;

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim("scope", "quotes.write")
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var expiresInSeconds = (int)options.AccessTokenLifetime.TotalSeconds;
        var expires = DateTime.UtcNow.Add(options.AccessTokenLifetime);

        var token = new JwtSecurityToken(
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return (accessToken, expiresInSeconds);
    }
}