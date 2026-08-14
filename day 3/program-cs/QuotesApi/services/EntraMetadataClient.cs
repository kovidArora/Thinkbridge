using QuotesApi.Options;
using Microsoft.Extensions.Options;

namespace QuotesApi.Services;

public interface IEntraMetadataClient
{
    Task<string> GetOpenIdConfigurationAsync(CancellationToken cancellationToken);
}

public class EntraMetadataClient : IEntraMetadataClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsSnapshot<EntraOptions> _entraOptions;

    public EntraMetadataClient(HttpClient httpClient, IOptionsSnapshot<EntraOptions> entraOptions)
    {
        _httpClient = httpClient;
        _entraOptions = entraOptions;
    }

    public async Task<string> GetOpenIdConfigurationAsync(CancellationToken cancellationToken)
    {
        var tenantId = _entraOptions.Value.TenantId;
        var response = await _httpClient.GetAsync(
            $"https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration",
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
