using System.Net.Http.Json;

namespace EasyLotteryWasm.Services;

public sealed class TunnelRuntimeClient
{
    private readonly HttpClient _httpClient;

    public TunnelRuntimeClient(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<TunnelRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        await _httpClient.GetFromJsonAsync<TunnelRuntimeStatus>("api/tunnel", cancellationToken) ?? new TunnelRuntimeStatus();

    public async Task<TunnelRuntimeStatus> StartAsync(string provider, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/tunnel/start", new TunnelStartRequest { Provider = provider }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TunnelRuntimeStatus>(cancellationToken: cancellationToken) ?? new TunnelRuntimeStatus();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.DeleteAsync("api/tunnel", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class TunnelRuntimeStatus
{
    public string Provider { get; set; } = "";
    public string State { get; set; } = "stopped";
    public string PublicBaseUrl { get; set; } = "";
    public string Error { get; set; } = "";
}

public sealed class TunnelStartRequest
{
    public string Provider { get; set; } = "";
}
