using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace EasyLotteryWasm.Services;

public sealed class PasskeyAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;

    public PasskeyAuthService(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    public Task<bool> HasSessionTokenAsync(CancellationToken cancellationToken = default) =>
        _jsRuntime.InvokeAsync<bool>("easyLotteryConfig.hasSessionToken", cancellationToken).AsTask();

    public async Task LoginAsync(string email, string flow, CancellationToken cancellationToken = default)
    {
        var begin = await SendAsync<PasskeyBeginResponse>(
            "api/auth/passkey/options",
            new { email, flow },
            cancellationToken);
        var credential = await _jsRuntime.InvokeAsync<JsonElement>(
            flow == "register" ? "easyLotteryConfig.createPasskey" : "easyLotteryConfig.getPasskey",
            cancellationToken,
            begin.Options);
        var result = await SendAsync<PasskeyTokenResponse>(
            "api/auth/passkey/verify",
            new { email, flow, credential },
            cancellationToken);
        await _jsRuntime.InvokeVoidAsync("easyLotteryConfig.setSessionToken", cancellationToken, result.Token);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await _jsRuntime.InvokeAsync<string>("easyLotteryConfig.getSessionToken", cancellationToken);
            using var request = new HttpRequestMessage(HttpMethod.Delete, "api/session");
            request.Headers.TryAddWithoutValidation("X-EasyLottery-Session-Token", token);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested is false)
        {
            // Clearing the browser token is still safe when server-side revocation is unavailable.
        }

        await _jsRuntime.InvokeVoidAsync("easyLotteryConfig.clearSessionToken", cancellationToken);
    }

    private async Task<T> SendAsync<T>(string path, object body, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(path, body, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Passkey API 未回傳內容。");
        }

        var error = await response.Content.ReadFromJsonAsync<PasskeyErrorResponse>(cancellationToken: cancellationToken);
        throw new PasskeyRequestException(
            response.StatusCode,
            error?.Code ?? "passkey_request_failed",
            error?.Error ?? $"Passkey API 回傳 HTTP {(int)response.StatusCode}。");
    }
}

public sealed record PasskeyBeginResponse(string Flow, JsonElement Options);
public sealed record PasskeyTokenResponse(string Token, DateTimeOffset ExpiresAtUtc);
public sealed record PasskeyErrorResponse(string? Error, string? Code);

public sealed class PasskeyRequestException : Exception
{
    public PasskeyRequestException(HttpStatusCode statusCode, string code, string message) : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public HttpStatusCode StatusCode { get; }
    public string Code { get; }
}
