using System.Net.Http.Json;
using EasyLotteryDomain.Models.Config;

namespace EasyLotteryWasm.Services;

/// <summary>Typed client for section settings resources and their ETag version.</summary>
public sealed class SettingsResourceApiClient(HttpClient httpClient, ObsSessionService sessionService)
{
    public Task<SettingsResourceSnapshot<ObsLayoutSettings>> GetObsLayoutAsync(CancellationToken cancellationToken = default) =>
        GetAsync<ObsLayoutSettings>("api/settings/obs-layout", cancellationToken);

    public Task<SettingsResourceSnapshot<ObsLayoutSettings>> SaveObsLayoutAsync(ObsLayoutSettings value, string? etag, CancellationToken cancellationToken = default) =>
        SaveAsync("api/settings/obs-layout", value, etag, cancellationToken);

    public Task<SettingsResourceSnapshot<SoundCueSettings>> GetSoundCueAsync(CancellationToken cancellationToken = default) =>
        GetAsync<SoundCueSettings>("api/settings/sound-cues", cancellationToken);

    public Task<SettingsResourceSnapshot<SoundCueSettings>> SaveSoundCueAsync(SoundCueSettings value, string? etag, CancellationToken cancellationToken = default) =>
        SaveAsync("api/settings/sound-cues", value, etag, cancellationToken);

    public Task<SettingsResourceSnapshot<VisualStyleSettings>> GetVisualStyleAsync(CancellationToken cancellationToken = default) =>
        GetAsync<VisualStyleSettings>("api/settings/visual-style", cancellationToken);

    public Task<SettingsResourceSnapshot<VisualStyleSettings>> SaveVisualStyleAsync(VisualStyleSettings value, string? etag, CancellationToken cancellationToken = default) =>
        SaveAsync("api/settings/visual-style", value, etag, cancellationToken);

    public Task<SettingsResourceSnapshot<PaymentSettingsResource>> GetPaymentsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<PaymentSettingsResource>("api/settings/payments", cancellationToken);

    public Task<SettingsResourceSnapshot<PaymentSettingsResource>> SavePaymentsAsync(PaymentSettingsResource value, string? etag, CancellationToken cancellationToken = default) =>
        SaveAsync("api/settings/payments", value, etag, cancellationToken);

    public Task<SettingsResourceSnapshot<OvertimeOverlaySettings>> GetOvertimeAsync(CancellationToken cancellationToken = default) =>
        GetAsync<OvertimeOverlaySettings>("api/settings/overtime", cancellationToken);

    public Task<SettingsResourceSnapshot<OvertimeOverlaySettings>> SaveOvertimeAsync(OvertimeOverlaySettings value, string? etag, CancellationToken cancellationToken = default) =>
        SaveAsync("api/settings/overtime", value, etag, cancellationToken);

    private async Task<SettingsResourceSnapshot<T>> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, path, cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return new SettingsResourceSnapshot<T>(
            await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken) ?? throw new InvalidOperationException("設定 API 未回傳內容。"),
            response.Headers.ETag?.ToString() ?? "");
    }

    private async Task<SettingsResourceSnapshot<T>> SaveAsync<T>(string path, T value, string? etag, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Put, path, cancellationToken);
        if (!string.IsNullOrWhiteSpace(etag)) request.Headers.TryAddWithoutValidation("If-Match", etag);
        request.Content = JsonContent.Create(value);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return new SettingsResourceSnapshot<T>(
            await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken) ?? value,
            response.Headers.ETag?.ToString() ?? etag ?? "");
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);
        var token = await sessionService.GetSessionTokenAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Add("X-EasyLottery-Session-Token", token);
        return request;
    }
}

public sealed record SettingsResourceSnapshot<T>(T Value, string ETag);
