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

    public async Task LoginAsync(string email, CancellationToken cancellationToken = default)
    {
        try
        {
            await LoginWithFlowAsync(email, "login", cancellationToken);
        }
        catch (PasskeyRequestException exception) when (exception.Code == "registration_required")
        {
            await LoginWithFlowAsync(email, "register", cancellationToken);
        }
    }

    public async Task<IReadOnlyList<PasskeyDevice>> ListPasskeysAsync(CancellationToken cancellationToken = default) =>
        await SendAuthorizedAsync<IReadOnlyList<PasskeyDevice>>(HttpMethod.Get, "api/admin/passkeys", null, cancellationToken);

    public async Task AddPasskeyAsync(string name, CancellationToken cancellationToken = default)
    {
        var begin = await SendAuthorizedAsync<PasskeyBeginResponse>(
            HttpMethod.Post,
            "api/admin/passkeys/options",
            new { name },
            cancellationToken);
        var credential = await _jsRuntime.InvokeAsync<JsonElement>(
            "easyLotteryConfig.createPasskey",
            cancellationToken,
            begin.Options);
        await SendAuthorizedAsync<PasskeyDevice>(
            HttpMethod.Post,
            "api/admin/passkeys/verify",
            new { credential },
            cancellationToken);
    }

    public async Task RemovePasskeyAsync(string id, CancellationToken cancellationToken = default) =>
        await SendAuthorizedNoContentAsync(HttpMethod.Delete, $"api/admin/passkeys/{Uri.EscapeDataString(id)}", cancellationToken);

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

    private async Task LoginWithFlowAsync(string email, string flow, CancellationToken cancellationToken)
    {
        var begin = await SendAsync<PasskeyBeginResponse>(
            HttpMethod.Post,
            "api/auth/passkey/options",
            new { email, flow },
            cancellationToken);
        var credential = await _jsRuntime.InvokeAsync<JsonElement>(
            flow == "register" ? "easyLotteryConfig.createPasskey" : "easyLotteryConfig.getPasskey",
            cancellationToken,
            begin.Options);
        var result = await SendAsync<PasskeyTokenResponse>(
            HttpMethod.Post,
            "api/auth/passkey/verify",
            new { email, flow, credential },
            cancellationToken);
        await _jsRuntime.InvokeVoidAsync("easyLotteryConfig.setSessionToken", cancellationToken, result.Token);
    }

    private async Task<T> SendAuthorizedAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(method, path, body, true, cancellationToken);
        return await SendAsync<T>(request, cancellationToken);
    }

    private async Task SendAuthorizedNoContentAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(method, path, null, true, cancellationToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) throw await CreateExceptionAsync(response, cancellationToken);
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object body,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(method, path, body, false, cancellationToken);
        return await SendAsync<T>(request, cancellationToken);
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException("Passkey API 未回傳內容。");
        }

        throw await CreateExceptionAsync(response, cancellationToken);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string path,
        object? body,
        bool authorized,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        if (authorized)
        {
            var token = await _jsRuntime.InvokeAsync<string>("easyLotteryConfig.getSessionToken", cancellationToken);
            request.Headers.TryAddWithoutValidation("X-EasyLottery-Session-Token", token);
        }

        return request;
    }

    private static async Task<PasskeyRequestException> CreateExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var error = await response.Content.ReadFromJsonAsync<PasskeyErrorResponse>(cancellationToken: cancellationToken);
        return new PasskeyRequestException(
            response.StatusCode,
            error?.Code ?? "passkey_request_failed",
            error?.Error ?? $"Passkey API 回傳 HTTP {(int)response.StatusCode}。");
    }
}

public sealed record PasskeyBeginResponse(string Flow, JsonElement Options);
public sealed record PasskeyTokenResponse(string Token, DateTimeOffset ExpiresAtUtc);
public sealed record PasskeyDevice(string Id, string Name, DateTimeOffset CreatedAtUtc);
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
