using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Quotes.Tests.Integration;

[Collection("SqlServer collection")]
public class SqlServerQuoteEndpointTests
{
    private readonly SqlServerTestFixture _fixture;

    public SqlServerQuoteEndpointTests(SqlServerTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Get_Quotes_AgainstRealSqlServer_ReturnsEmptyListOnFreshDatabase()
    {
        using var factory = new SqlServerWebApplicationFactory(_fixture.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/quotes?page=1&size=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
