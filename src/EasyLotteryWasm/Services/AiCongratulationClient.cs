using System.Net.Http.Json;

namespace EasyLotteryWasm.Services;

public sealed class AiCongratulationClient(HttpClient client, ObsSessionService session)
{
    public async Task<string?> GenerateAsync(Guid activityPublicId, string donorName, string prizeName, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/polaroid-congratulation") { Content = JsonContent.Create(new { activityPublicId, donorName, prizeName }) };
        request.Headers.Add(ObsSessionTokenHeader.Name, await session.GetSessionTokenAsync(cancellationToken));
        using var response = await client.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        return (await response.Content.ReadFromJsonAsync<Response>(cancellationToken))?.message?.Trim();
    }

    private sealed class Response { public string? message { get; set; } }
}

internal static class ObsSessionTokenHeader { public const string Name = "X-EasyLottery-Session-Token"; }
