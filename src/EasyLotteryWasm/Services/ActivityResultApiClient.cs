using System.Net.Http.Json;
using EasyLotteryDomain.Models.Config;

namespace EasyLotteryWasm.Services;

public sealed class ActivityResultApiClient(HttpClient client, ObsSessionService session)
{
    public async Task<List<ActivityResultRecord>> ListActivityResultsAsync(CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/activity-results", null, ct);
        return await response.Content.ReadFromJsonAsync<List<ActivityResultRecord>>(cancellationToken: ct) ?? [];
    }
    public async Task<ActivityResultRecord?> LoadActivityResultAsync(int id, CancellationToken ct = default)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/activity-results/{id}", null, ct);
        return await response.Content.ReadFromJsonAsync<ActivityResultRecord>(cancellationToken: ct);
    }
    public async Task<ActivityResultRecord> RecordPokeActivityAsync(int templateId, Guid? publicId = null, CancellationToken ct = default)
    {
        var path = publicId.HasValue ? $"api/poke-templates/public/{publicId.Value}/record-result" : "api/activity-results/poke";
        using var response = await SendAsync(HttpMethod.Post, path, publicId.HasValue ? null : new { templateId }, ct);
        return await response.Content.ReadFromJsonAsync<ActivityResultRecord>(cancellationToken: ct) ?? throw new InvalidOperationException("API 未回傳活動結果。");
    }
    public async Task<ActivityResultRecord> RecordRouletteActivityAsync(int templateId, EasyLotteryDomain.Models.Entities.SpinResult spinResult, Guid? publicId = null, CancellationToken ct = default)
    {
        var path = publicId.HasValue ? $"api/roulette-templates/public/{publicId.Value}/record-result" : "api/activity-results/roulette";
        using var response = await SendAsync(HttpMethod.Post, path, publicId.HasValue ? spinResult : new { templateId, spinResult }, ct);
        return await response.Content.ReadFromJsonAsync<ActivityResultRecord>(cancellationToken: ct) ?? throw new InvalidOperationException("API 未回傳活動結果。");
    }

    public List<ActivityResultRecord> FilterActivityResults(IEnumerable<ActivityResultRecord> records, string? searchText = null, ActivityResultType? activityType = null, DateOnly? startDate = null, DateOnly? endDate = null) =>
        EasyLotteryDomain.Services.ActivityResultService.FilterActivityResults(records, searchText, activityType, startDate, endDate);

    public string SerializeActivityResults(IEnumerable<ActivityResultRecord> records) =>
        EasyLotteryDomain.Services.ActivityResultService.SerializeActivityResults(records);

    public string SerializeActivityResultsCsv(IEnumerable<ActivityResultRecord> records) =>
        EasyLotteryDomain.Services.ActivityResultService.SerializeActivityResultsCsv(records);
    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(ObsSessionTokenHeader.Name, await session.GetSessionTokenAsync(ct));
        if (body is not null) request.Content = JsonContent.Create(body);
        var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return response;
    }
}
