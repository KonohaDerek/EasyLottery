using System.Net.Http.Json;

namespace EasyLotteryWasm.Services;

public sealed class InteractionApiClient(HttpClient httpClient, ObsSessionService sessionService)
{
    public async Task<IReadOnlyList<InteractionRoundResource>> ListRoundsAsync(CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, "api/interactions/rounds", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<InteractionRoundResource>>(cancellationToken: cancellationToken) ?? [];
    }

    public async Task CreateAsync(CreateInteractionRoundResource value, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Post, "api/interactions/rounds", cancellationToken);
        request.Content = JsonContent.Create(value);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task TransitionAsync(Guid id, string action, CancellationToken cancellationToken = default)
    {
        using var request = await CreateRequestAsync(HttpMethod.Post, $"api/interactions/rounds/{id}/{action}", cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);
        var token = await sessionService.GetSessionTokenAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Add("X-EasyLottery-Session-Token", token);
        return request;
    }
}

public sealed record InteractionRoundResource(Guid Id, string Name, string Status, string Type, string CommandPrefix, IReadOnlyList<string> VoteOptions, int CorrectAnswerPoints, int EligibilityTickets);
public sealed record CreateInteractionRoundResource(string Name, string Type, string CommandPrefix, string[] VoteOptions, string? CorrectAnswer, int CorrectAnswerPoints = 0, int EligibilityTickets = 0);
