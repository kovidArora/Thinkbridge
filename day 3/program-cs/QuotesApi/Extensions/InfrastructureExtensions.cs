using Microsoft.EntityFrameworkCore;
using QuotesApi.Commands;
using QuotesApi.Data;
using QuotesApi.Queries;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<QuotesDbContext>(options =>
            options.UseSqlite(
                configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<CreateQuoteCommandHandler>();
        services.AddScoped<QuoteReadModel>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }
}