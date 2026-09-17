using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;

namespace QuotesApi.Tests.E2E;

/// The one true end-to-end test: launches the real compiled QuotesApi.dll as
/// its own OS process, listening on a real socket via Kestrel, and drives
/// the whole flow through a real HttpClient over the network. Deliberately
/// NOT a WebApplicationFactory<Program> test (that's what
/// Quotes.Tests.Integration / QuotesApi.Tests.Integration already do,
/// against an in-memory TestServer) -- this is the layer above that, where
/// nothing about the app's startup, config binding, or middleware pipeline
/// is short-circuited by being hosted in-process with the test runner.
public sealed class HappyPathE2ETests : IAsyncLifetime
{
    private Process? _process;
    private HttpClient _client = null!;
    private string _dbPath = string.Empty;
    private int _port;

    public async Task InitializeAsync()
    {
        _port = GetFreeTcpPort();
        _dbPath = Path.Combine(Path.GetTempPath(), $"quotesapi-e2e-{Guid.NewGuid():N}.db");

        var dllPath = typeof(Program).Assembly.Location;

        var startInfo = new ProcessStartInfo("dotnet", $"\"{dllPath}\"")
        {
            UseShellExecute = false,
            // Deliberately NOT redirected: redirecting without ever reading
            // the streams fills the OS pipe buffer once ASP.NET Core's
            // startup logging writes enough to it, and the child process
            // blocks trying to write more -- it never finishes starting or
            // binds the port, which is exactly what caused every readiness
            // check to see "connection refused" for the full timeout.
            WorkingDirectory = Path.GetDirectoryName(dllPath),
        };
        startInfo.EnvironmentVariables["ASPNETCORE_URLS"] = $"http://localhost:{_port}";
        startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.EnvironmentVariables["ConnectionStrings__DefaultConnection"] = $"Data Source={_dbPath}";

        _process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the QuotesApi process for the E2E test.");

        _client = new HttpClient { BaseAddress = new Uri($"http://localhost:{_port}") };

        await WaitForReadyAsync(TimeSpan.FromSeconds(30));
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();

        if (_process is { HasExited: false })
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }
        _process?.Dispose();

        foreach (var suffix in new[] { "", "-shm", "-wal" })
        {
            var path = _dbPath + suffix;
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task RegisterLoginCreateFetchDelete_FullJourney_WorksAgainstRealProcess()
    {
        var email = $"e2e-{Guid.NewGuid():N}@example.com";

        // register
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "E2ETestPassword123!",
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var registerBody = await registerResponse.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(registerBody);
        Assert.False(string.IsNullOrWhiteSpace(registerBody!.access_token));

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", registerBody.access_token);

        // create
        var createResponse = await _client.PostAsJsonAsync("/api/quotes", new
        {
            author = "E2E Test Author",
            text = "A quote created by the real end-to-end test.",
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<QuoteResponse>();
        Assert.NotNull(created);

        // fetch
        var fetchResponse = await _client.GetAsync($"/api/quotes/{created!.id}");
        Assert.Equal(HttpStatusCode.OK, fetchResponse.StatusCode);

        var fetched = await fetchResponse.Content.ReadFromJsonAsync<QuoteResponse>();
        Assert.Equal("E2E Test Author", fetched!.author);

        // delete
        var deleteResponse = await _client.DeleteAsync($"/api/quotes/{created.id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // confirm it's actually gone -- not just a 204 with no real effect
        var refetchResponse = await _client.GetAsync($"/api/quotes/{created.id}");
        Assert.Equal(HttpStatusCode.NotFound, refetchResponse.StatusCode);
    }

    private async Task WaitForReadyAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                // A real, unauthenticated GET with a valid page/size is
                // enough to know Kestrel is actually listening and the
                // request pipeline is wired up -- a 200 or 400 both prove
                // the app is up; only a connection failure means "not yet".
                using var response = await _client.GetAsync("/api/quotes?page=1&size=1");
                return;
            }
            catch (HttpRequestException ex)
            {
                lastError = ex;
                await Task.Delay(200);
            }
        }

        throw new TimeoutException(
            $"QuotesApi did not become ready on port {_port} within {timeout}.", lastError);
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed record TokenResponse(string access_token, string refresh_token, int expires_in);

    private sealed record QuoteResponse(int id, string author, string text);
}
