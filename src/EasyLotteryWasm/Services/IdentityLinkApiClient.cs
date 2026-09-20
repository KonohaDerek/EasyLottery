using System.Net.Http.Json;

namespace EasyLotteryWasm.Services;

public sealed class IdentityLinkApiClient(HttpClient httpClient)
{
    public async Task CompleteAsync(string code, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("api/interactions/identity-links/complete", new { code }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
