using System.Net.Http.Json;
using System.Text;
using EasyLotteryDomain.Models.Entities;

namespace EasyLotteryWasm.Services;

public sealed class PokeTemplateApiClient(HttpClient client, ObsSessionService session)
{
    private Guid? publicId;

    public async Task<List<PokeTemplate>> ListTemplatesAsync(CancellationToken ct = default)
    {
        publicId = null;
        return await SendJsonAsync<List<PokeTemplate>>(HttpMethod.Get, "api/poke-templates", null, ct) ?? [];
    }
    public async Task<PokeTemplate?> LoadTemplateAsync(int id, CancellationToken ct = default)
    {
        publicId = null;
        return await SendJsonAsync<PokeTemplate>(HttpMethod.Get, $"api/poke-templates/{id}", null, ct);
    }
    public async Task<PokeTemplate?> LoadTemplateAsync(Guid id, CancellationToken ct = default)
    {
        publicId = id;
        return await SendJsonAsync<PokeTemplate>(HttpMethod.Get, $"api/poke-templates/public/{id}", null, ct);
    }
    public Task<PokeTemplate> CreateTemplateAsync(PokeTemplate template, CancellationToken ct = default) => SendJsonAsync<PokeTemplate>(HttpMethod.Post, "api/poke-templates", template, ct)!;
    public Task<PokeTemplate> UpdateTemplateAsync(PokeTemplate template, CancellationToken ct = default) => SendJsonAsync<PokeTemplate>(HttpMethod.Put, $"api/poke-templates/{template.Id}", template, ct)!;
    public async Task DeleteTemplateAsync(int id, CancellationToken ct = default) => await SendNoContentAsync(HttpMethod.Delete, $"api/poke-templates/{id}", null, ct);
    public Task<PokeTemplate> DuplicateTemplateAsync(int id, CancellationToken ct = default) => SendJsonAsync<PokeTemplate>(HttpMethod.Post, $"api/poke-templates/{id}/duplicate", null, ct)!;
    public Task<PokeTemplate> SetPublicationStatusAsync(int id, TemplatePublicationStatus status, CancellationToken ct = default) => SendJsonAsync<PokeTemplate>(HttpMethod.Put, $"api/poke-templates/{id}/publication-status", new { status }, ct)!;
    public Task<PokeCell?> PokeRandomCellAsync(int id, CancellationToken ct = default) => SendJsonAsync<PokeCell>(HttpMethod.Post, ActionPath(id, "draw"), new { index = (int?)null }, ct);
    public Task<PokeCell?> PokeCellByIndexAsync(int id, int index, CancellationToken ct = default) => SendJsonAsync<PokeCell>(HttpMethod.Post, ActionPath(id, "draw"), new { index = (int?)index }, ct);
    public async Task<List<PokeCell>> GetRevealStateAsync(int id, CancellationToken ct = default) => await SendJsonAsync<List<PokeCell>>(HttpMethod.Get, $"api/poke-templates/{id}/reveal-state", null, ct) ?? [];
    public async Task ResetTemplateAsync(int id, CancellationToken ct = default) => await SendNoContentAsync(HttpMethod.Post, ActionPath(id, "reset"), null, ct);
    public async Task<string> ExportTemplateAsync(int id, CancellationToken ct = default) => await SendTextAsync($"api/poke-templates/{id}/export", ct);
    public async Task<PokeTemplate> ImportTemplateAsync(string json, CancellationToken ct = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Post, "api/poke-templates/import", ct);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<PokeTemplate>(cancellationToken: ct) ?? throw new InvalidOperationException("API 未回傳戳戳樂模板。");
    }
    public async Task SeedDefaultTemplatesAsync(CancellationToken ct = default) => await SendNoContentAsync(HttpMethod.Post, "api/poke-templates/seed-defaults", null, ct);

    private string ActionPath(int id, string action) => publicId.HasValue ? $"api/poke-templates/public/{publicId.Value}/{action}" : $"api/poke-templates/{id}/{action}";

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
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return default;
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
