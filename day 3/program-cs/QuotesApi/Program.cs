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
using Serilog;
using Serilog.Context;
using OpenTelemetry.Trace;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.Options;
using QuotesApi;
using QuotesApi.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<EntraOptions>(builder.Configuration.GetSection("Entra"));

builder.Services.AddSingleton(Telemetry.ActivitySource);

var otelBuilder = builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddSource(Telemetry.ServiceName)
        .AddAspNetCoreInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"]
    ?? builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    otelBuilder.UseAzureMonitor(options =>
    {
        options.ConnectionString = appInsightsConnectionString;
    });
}

builder.Services.AddEntraMetadataClient();

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
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<Program>>();

            logger.LogWarning(
                context.Exception,
                "Internal JWT authentication failed for {Path}",
                context.HttpContext.Request.Path);

            return Task.CompletedTask;
        }
    };
})
    .AddJwtBearer(EntraScheme, options =>
{
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<Program>>();

            logger.LogWarning(
                context.Exception,
                "Entra JWT authentication failed for {Path}",
                context.HttpContext.Request.Path);

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

builder.Services.AddOptions<JwtBearerOptions>(InternalScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.Value.Key)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true
        };
    });

builder.Services.AddOptions<JwtBearerOptions>(EntraScheme)
    .Configure<IOptions<EntraOptions>>((options, entraOptionsAccessor) =>
    {
        var entraOptions = entraOptionsAccessor.Value;
        var tenantId = entraOptions.TenantId;
        var audience = entraOptions.Audience;

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
    });

builder.Services.AddHealthChecks();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("can-edit-quotes", policy =>
        policy.RequireClaim("scope", "quotes.write"));

    options.AddPolicy("must-own-quote", policy =>
        policy.Requirements.Add(new MustOwnQuoteRequirement()));
});

var app = builder.Build();

app.Use((context, next) =>
{
    var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString()
        ?? context.TraceIdentifier;

    using (LogContext.PushProperty("TraceId", traceId))
    {
        return next();
    }
});

app.UseSerilogRequestLogging();

app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

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

    if (app.Environment.IsDevelopment() && !db.Quotes.Any())
    {
        var seedUserId = db.Users.Select(u => u.Id).First();
        var rnd = new Random(1234);
        const int authorCount = 300;
        const int quoteCount = 20_000;

        for (var i = 1; i <= quoteCount; i++)
        {
            var author = $"Author {rnd.Next(1, authorCount + 1)}";
            var (quote, _) = Quote.Create(author, $"Quote number {i}", seedUserId);
            db.Quotes.Add(quote!);

            if (i % 2000 == 0)
            {
                db.SaveChanges();
            }
        }

        db.SaveChanges();
    }
}

app.MapQuoteEndpoints();
app.MapAuthEndpoints();

app.Run();

public partial class Program { }