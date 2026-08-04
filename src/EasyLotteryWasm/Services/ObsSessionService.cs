using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace EasyLotteryWasm.Services;

public sealed class ObsSessionService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly HttpClient _httpClient;
    public ObsSessionService(IJSRuntime jsRuntime, HttpClient httpClient)
    {
        _jsRuntime = jsRuntime;
        _httpClient = httpClient;
    }

    public async Task<string> GetSessionTokenAsync(CancellationToken cancellationToken = default)
    {
        return (await _jsRuntime.InvokeAsync<string>("easyLotteryConfig.getSessionToken", cancellationToken)).Trim();
    }

    public async Task<string> CreateObsTokenAsync(
        string resourceKind,
        string resourceId,
        bool allowControl,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/obs-sessions");
        request.Headers.Add(ObsSessionTokenHeader.Name, await GetSessionTokenAsync(cancellationToken));
        request.Content = JsonContent.Create(new ObsTokenRequest(
            resourceKind,
            resourceId,
            allowControl ? ["read", "control"] : ["read"]));
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SessionTokenResponse>(cancellationToken: cancellationToken);
        return result?.Token ?? throw new InvalidOperationException("OBS token endpoint 未回傳 token。");
    }

    private sealed record ObsTokenRequest(string ResourceKind, string ResourceId, string[] Scopes);
    private sealed record SessionTokenResponse(string Token, DateTimeOffset ExpiresAtUtc);
}
