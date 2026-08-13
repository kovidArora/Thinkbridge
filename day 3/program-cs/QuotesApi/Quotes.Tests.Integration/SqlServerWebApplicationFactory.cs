using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuotesApi.Data;
using QuotesApi.Repositories;

namespace Quotes.Tests.Integration;

public class SqlServerWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    public FakeClock Clock { get; } = new FakeClock();

    public SqlServerWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.ConfigureAppConfiguration((_, config) =>
    {
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "this-is-a-32-byte-secret-key-1234"
        });
    });

    builder.ConfigureServices(services =>
    {

        var descriptorsToRemove = services
            .Where(d =>
                d.ServiceType == typeof(DbContextOptions<QuotesDbContext>) ||
                d.ServiceType == typeof(QuotesDbContext) ||
                (d.ServiceType.FullName?.Contains("EntityFrameworkCore") ?? false))
            .ToList();

        foreach (var descriptor in descriptorsToRemove)
        {
            services.Remove(descriptor);
        }

    services.AddDbContext<QuotesDbContext>(options =>
{
    options.UseSqlServer(_connectionString);
    options.ConfigureWarnings(w =>
        w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
});

        var clockDescriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(IClock));
        if (clockDescriptor is not null)
        {
            services.Remove(clockDescriptor);
        }

        services.AddSingleton<IClock>(Clock);
    });
}
}
