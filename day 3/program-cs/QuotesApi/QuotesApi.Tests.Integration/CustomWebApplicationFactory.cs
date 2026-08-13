using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QuotesApi.Data;

namespace QuotesApi.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";
    private Microsoft.Data.Sqlite.SqliteConnection? _connection;

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
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<QuotesDbContext>));

            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }
            _connection = new Microsoft.Data.Sqlite.SqliteConnection(
                $"DataSource=file:{_dbName}?mode=memory&cache=shared");
            _connection.Open();

            services.AddDbContext<QuotesDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });


        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection?.Dispose();
    }
}