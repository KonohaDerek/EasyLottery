using System.Net;
using System.Net.Http.Json;
using EasyLotteryApi.Security;
using EasyLotteryApi.Security.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EasyLotteryApiTests.Interactions;

[TestClass]
public sealed class InteractionRoundEndpointTests
{
    [TestMethod]
    public async Task RoundLifecycle_RequiresAdminAndFollowsAllowedOrdering()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var denied = await client.PostAsJsonAsync("/api/interactions/rounds", new { name = "投票", type = "vote", commandPrefix = "!vote", voteOptions = new[] { "a", "b" } });
        Assert.AreEqual(HttpStatusCode.Unauthorized, denied.StatusCode);

        AddToken(client, LoginAsync(factory));
        using var created = await client.PostAsJsonAsync("/api/interactions/rounds", new { name = "投票", type = "vote", commandPrefix = "!vote", voteOptions = new[] { "a", "b" } });
        created.EnsureSuccessStatusCode();
        var round = await created.Content.ReadFromJsonAsync<RoundResource>();

        (await client.PostAsync($"/api/interactions/rounds/{round!.Id}/start", null)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/interactions/rounds/{round.Id}/pause", null)).EnsureSuccessStatusCode();
        using var pausedEvent = await client.PostAsJsonAsync($"/api/interactions/rounds/{round.Id}/events", new { audienceProfileId = Guid.NewGuid(), platform = "YouTube", channelScope = "channel", externalUserId = "viewer", externalEventId = "event-1", content = "!vote a" });
        var pausedBody = await pausedEvent.Content.ReadAsStringAsync();
        Assert.IsTrue(pausedBody.Contains("round-not-live", StringComparison.Ordinal));
        (await client.PostAsync($"/api/interactions/rounds/{round.Id}/settle", null)).EnsureSuccessStatusCode();
        using var restart = await client.PostAsync($"/api/interactions/rounds/{round.Id}/start", null);
        Assert.AreEqual(HttpStatusCode.BadRequest, restart.StatusCode);

        using var adjustment = await client.PostAsJsonAsync($"/api/interactions/rounds/{round.Id}/points", new { audienceProfileId = Guid.NewGuid(), pointDelta = 5, reason = "主持人補分" });
        Assert.AreEqual(HttpStatusCode.NoContent, adjustment.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:EnableAdminSessionToken"] = "true",
                ["AdminAuth:SigningKey"] = "test-signing-key-for-interaction-rounds-1234567890"
            })));

    private static string LoginAsync(WebApplicationFactory<Program> factory)
    {
        using var client = factory.CreateClient();
        using var response = client.GetAsync("/api/test/session-token").GetAwaiter().GetResult();
        return response.Content.ReadFromJsonAsync<SessionTokenResponse>().GetAwaiter().GetResult()!.Token;
    }

    private static void AddToken(HttpClient client, string token) => client.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, token);

    private sealed record RoundResource(Guid Id, string Name, string Status);
}
