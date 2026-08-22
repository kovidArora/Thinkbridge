using Dapper;
using Microsoft.Data.Sqlite;
using QuotesApi.Repositories;

namespace QuotesApi.Queries;

public class AuthorStatsDapperQuery
{
    private readonly string _connectionString;

    public AuthorStatsDapperQuery(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")!;
    }

    public async Task<List<AuthorStatsDto>> GetAuthorStatsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Author, COUNT(*) AS QuoteCount
            FROM Quotes
            WHERE IsDeleted = 0
            GROUP BY Author
            """;

        await using var connection = new SqliteConnection(_connectionString);
        var rows = await connection.QueryAsync<AuthorStatsDto>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return rows.AsList();
    }
}
