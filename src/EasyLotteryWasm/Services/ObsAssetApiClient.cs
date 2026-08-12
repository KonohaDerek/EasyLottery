using System.Net.Http.Headers;
using System.Net.Http.Json;
using EasyLotteryDomain.Models.Obs;

namespace EasyLotteryWasm.Services;

public sealed class ObsAssetApiClient(HttpClient client, ObsSessionService session)
{
    public async Task<IReadOnlyList<ObsAsset>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "api/obs-assets", cancellationToken);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ObsAsset>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<ObsAsset>> ListUnusedAsync(CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "api/obs-assets/unused", cancellationToken);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ObsAsset>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<ObsAssetLimitValues> GetLimitsAsync(CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "api/obs-assets/limits", cancellationToken);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ObsAssetLimitValues>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("資產限制 API 未回傳內容。");
    }

    public async Task<byte[]> ExportPackageAsync(CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "api/obs-assets/export", cancellationToken);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ObsAsset>> ImportPackageAsync(Stream package, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Post, "api/obs-assets/import", cancellationToken);
        request.Content = new StreamContent(package);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<ObsAsset>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task<ObsAsset> UploadAsync(string fileName, string contentType, long length, Stream content, ObsAssetKind kind, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Post, "api/obs-assets", cancellationToken);
        using var form = new MultipartFormDataContent();
        var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        form.Add(streamContent, "file", fileName);
        form.Add(new StringContent(kind.ToString()), "kind");
        request.Content = form;
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ObsAsset>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("資產 API 未回傳內容。");
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Delete, $"api/obs-assets/{id:D}", cancellationToken);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public string GetContentUrl(Guid id) => new Uri(client.BaseAddress!, $"api/obs-assets/{id:D}/content").ToString();

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);
        var token = await session.GetSessionTokenAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Add(ObsTokenHeaderName, token);
        return request;
    }

    private const string ObsTokenHeaderName = "X-EasyLottery-Session-Token";
}
