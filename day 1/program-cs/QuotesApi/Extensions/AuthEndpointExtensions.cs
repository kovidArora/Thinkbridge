using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Services;

namespace QuotesApi.Extensions;

public static class AuthEndpointExtensions
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            QuotesDbContext db,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService refreshTokenService) =>
        {
            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (user is null ||
                !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return Results.Unauthorized();
            }

            var (accessToken, expiresIn) = jwtTokenService.GenerateAccessToken(user);
            var (refreshToken, _) = await refreshTokenService.GenerateAsync(user.Id);

            return Results.Ok(new
            {
                access_token = accessToken,
                refresh_token = refreshToken,
                expires_in = expiresIn
            });
        });

        app.MapPost("/api/auth/refresh", async (
            RefreshRequest request,
            QuotesDbContext db,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService refreshTokenService) =>
        {
            var result = await refreshTokenService.ValidateAndRotateAsync(request.RefreshToken);

            if (!result.Succeeded)
            {
                return Results.Unauthorized();
            }

            var user = await db.Users.FindAsync(result.UserId);

            if (user is null)
            {
                return Results.Unauthorized();
            }

            var (accessToken, expiresIn) = jwtTokenService.GenerateAccessToken(user);

            return Results.Ok(new
            {
                access_token = accessToken,
                refresh_token = result.NewRawToken,
                expires_in = expiresIn
            });
        });

        app.MapPost("/api/auth/logout", async (
            RefreshRequest request,
            IRefreshTokenService refreshTokenService) =>
        {
            await refreshTokenService.RevokeAsync(request.RefreshToken);
            return Results.NoContent();
        });
    }
}

public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);