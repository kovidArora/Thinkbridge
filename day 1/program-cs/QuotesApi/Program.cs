using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Extensions;
using QuotesApi.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

// Apply any pending EF Core migrations
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
    db.Database.Migrate();
}

app.MapQuoteEndpoints();

app.Run();
public partial class Program { }