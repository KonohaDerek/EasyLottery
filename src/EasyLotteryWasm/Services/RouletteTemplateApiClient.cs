using System.Net.Http.Json;
using System.Text;
using EasyLotteryDomain.Models.Entities;
using EasyLotteryDomain.Services;

namespace EasyLotteryWasm.Services;

public sealed class RouletteTemplateApiClient(HttpClient client, ObsSessionService session)
{
    private Guid? publicId;

    public async Task<List<RouletteTemplate>> ListTemplatesAsync(CancellationToken ct = default)
    {
        publicId = null;
        return await SendJsonAsync<List<RouletteTemplate>>(HttpMethod.Get, "api/roulette-templates", null, ct) ?? [];
    }
    public async Task<RouletteTemplate?> LoadTemplateAsync(int id, CancellationToken ct = default)
    {
        publicId = null;
        return await SendJsonAsync<RouletteTemplate>(HttpMethod.Get, $"api/roulette-templates/{id}", null, ct);
    }
    public async Task<RouletteTemplate?> LoadTemplateAsync(Guid id, CancellationToken ct = default)
    {
        publicId = id;
        return await SendJsonAsync<RouletteTemplate>(HttpMethod.Get, $"api/roulette-templates/public/{id}", null, ct);
    }
    public Task<RouletteTemplate> CreateTemplateAsync(RouletteTemplate template, CancellationToken ct = default) => SendJsonAsync<RouletteTemplate>(HttpMethod.Post, "api/roulette-templates", template, ct)!;
    public Task<RouletteTemplate> UpdateTemplateAsync(RouletteTemplate template, CancellationToken ct = default) => SendJsonAsync<RouletteTemplate>(HttpMethod.Put, $"api/roulette-templates/{template.Id}", template, ct)!;
    public async Task DeleteTemplateAsync(int id, CancellationToken ct = default) => await SendNoContentAsync(HttpMethod.Delete, $"api/roulette-templates/{id}", null, ct);
    public Task<RouletteTemplate> DuplicateTemplateAsync(int id, CancellationToken ct = default) => SendJsonAsync<RouletteTemplate>(HttpMethod.Post, $"api/roulette-templates/{id}/duplicate", null, ct)!;
    public Task<RouletteTemplate> SetPublicationStatusAsync(int id, TemplatePublicationStatus status, CancellationToken ct = default) => SendJsonAsync<RouletteTemplate>(HttpMethod.Put, $"api/roulette-templates/{id}/publication-status", new { status }, ct)!;
    public Task<SpinResult> SpinAsync(int id, int? forceIndex = null, double currentRotation = 0, CancellationToken ct = default) => SendJsonAsync<SpinResult>(HttpMethod.Post, SpinPath(id), new { forceIndex, currentRotation }, ct)!;
    public async Task<string> ExportTemplateAsync(int id, CancellationToken ct = default) => await SendTextAsync($"api/roulette-templates/{id}/export", ct);
    public async Task<RouletteTemplate> ImportTemplateAsync(string json, CancellationToken ct = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Post, "api/roulette-templates/import", ct);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RouletteTemplate>(cancellationToken: ct) ?? throw new InvalidOperationException("API 未回傳轉盤模板。");
    }
    public async Task SeedDefaultTemplatesAsync(CancellationToken ct = default) => await SendNoContentAsync(HttpMethod.Post, "api/roulette-templates/seed-defaults", null, ct);
    public Task<SpinResult> Spin(RouletteTemplate template, int? forceIndex = null, double currentRotation = 0) => SpinAsync(template.Id, forceIndex, currentRotation);

    private string SpinPath(int id) => publicId.HasValue ? $"api/roulette-templates/public/{publicId.Value}/spin" : $"api/roulette-templates/{id}/spin";
    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, path);
        var token = await session.GetSessionTokenAsync(ct);
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Add(ObsSessionTokenHeader.Name, token);
        return request;
    }
    private async Task<T?> SendJsonAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = await CreateRequestAsync(method, path, ct);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }
    private async Task SendNoContentAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var request = await CreateRequestAsync(method, path, ct);
        if (body is not null) request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
    private async Task<string> SendTextAsync(string path, CancellationToken ct)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, path, ct);
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
