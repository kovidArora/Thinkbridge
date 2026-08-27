using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Services;

namespace QuotesApi.Extensions;

public static class AuthEndpointExtensions
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/register", async (
            RegisterRequest request,
            QuotesDbContext db,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService refreshTokenService) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = ["A valid email is required."]
                });
            }

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["password"] = ["Password must be at least 8 characters."]
                });
            }

            var existing = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);

            if (existing is not null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["email"] = ["An account with this email already exists."]
                });
            }

            var user = new User
            {
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            };

            db.Users.Add(user);
            await db.SaveChangesAsync();

            var (accessToken, expiresIn) = jwtTokenService.GenerateAccessToken(user);
            var (refreshToken, _) = await refreshTokenService.GenerateAsync(user.Id);

            return Results.Created("/api/auth/register", new
            {
                access_token = accessToken,
                refresh_token = refreshToken,
                expires_in = expiresIn
            });
        });

        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            QuotesDbContext db,
            IJwtTokenService jwtTokenService,
            IRefreshTokenService refreshTokenService,
            ActivitySource activitySource) =>
        {
            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            bool passwordValid;
            using (var activity = activitySource.StartActivity("verify-password"))
            {
                activity?.SetTag("user.id", user?.Id);
                passwordValid = user is not null &&
                    BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
            }

            if (user is null || !passwordValid)
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
public record RegisterRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);