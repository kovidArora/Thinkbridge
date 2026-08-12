using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Extensions;
using QuotesApi.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using QuotesApi.Models;
using Microsoft.AspNetCore.Authorization;
using QuotesApi.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthorizationHandler, MustOwnQuoteHandler>();

const string InternalScheme = "Internal";
const string EntraScheme = "Entra";

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = "smart";
        options.DefaultChallengeScheme = "smart";
    })
    .AddJwtBearer(InternalScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(
                    builder.Configuration["Jwt:Key"]!)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true
        };
    })
    .AddJwtBearer(EntraScheme, options =>
{
    var tenantId = builder.Configuration["Entra:TenantId"];
    var audience = builder.Configuration["Entra:Audience"];

    options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
    options.Audience = audience;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuers = new[]
        {
            $"https://login.microsoftonline.com/{tenantId}/v2.0",
            $"https://sts.windows.net/{tenantId}/"
        },
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine("=== ENTRA AUTH FAILED ===");
            Console.WriteLine(context.Exception.ToString());
            return Task.CompletedTask;
        }
    };
})
    .AddPolicyScheme("smart", "Internal or Entra JWT", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authHeader = context.Request.Headers["Authorization"].ToString();

            if (authHeader.StartsWith("Bearer "))
            {
                var token = authHeader["Bearer ".Length..];

                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    var jwt = handler.ReadJwtToken(token);
                    var issuer = jwt.Issuer;

                    if (issuer.Contains("login.microsoftonline.com") ||
                        issuer.Contains("sts.windows.net"))
                    {
                        return EntraScheme;
                    }
                }
                catch
                {
                    // Malformed token falls through to Internal,
                    // which will correctly reject it with 401.
                }
            }

            return InternalScheme;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("can-edit-quotes", policy =>
        policy.RequireClaim("scope", "quotes.write"));

    options.AddPolicy("must-own-quote", policy =>
        policy.Requirements.Add(new MustOwnQuoteRequirement()));
});

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Apply any pending EF Core migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();

    db.Database.Migrate();

    // Temporary test user
    if (!db.Users.Any())
{
    db.Users.Add(new User
    {
        Email = "test@example.com",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!")
    });

    db.Users.Add(new User
    {
        Email = "second@example.com",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!")
    });

    db.SaveChanges();
}
}

app.MapQuoteEndpoints();
app.MapAuthEndpoints();

app.Run();

public partial class Program { }