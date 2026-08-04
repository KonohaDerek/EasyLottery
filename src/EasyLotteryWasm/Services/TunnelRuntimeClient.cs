using System.Net.Http.Json;

namespace EasyLotteryWasm.Services;

public sealed class TunnelRuntimeClient
{
    private readonly HttpClient _httpClient;
    private readonly ObsSessionService _sessionService;

    public TunnelRuntimeClient(HttpClient httpClient, ObsSessionService sessionService)
    {
        _httpClient = httpClient;
        _sessionService = sessionService;
    }

    public async Task<TunnelRuntimeStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "api/tunnel", cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TunnelRuntimeStatus>(cancellationToken: cancellationToken) ?? new TunnelRuntimeStatus();
    }

    public async Task<TunnelRuntimeStatus> StartAsync(string provider, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Post, "api/tunnel/start", cancellationToken);
        request.Content = JsonContent.Create(new TunnelStartRequest { Provider = provider });
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TunnelRuntimeStatus>(cancellationToken: cancellationToken) ?? new TunnelRuntimeStatus();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Delete, "api/tunnel", cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(ObsSessionTokenHeader.Name, await _sessionService.GetSessionTokenAsync(cancellationToken));
        return request;
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
