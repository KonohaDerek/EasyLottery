using System.Net;
using System.Net.Http.Json;
using EasyLotteryApi.Security;
using EasyLotteryApi.Security.Endpoints;
using EasyLotteryInfrastructure.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EasyLotteryApiTests.Interactions;

[TestClass]
public sealed class PlatformConnectionEndpointTests
{
    [TestMethod]
    public async Task Connections_GetMasksSecretsAndReportsHealth()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        AddToken(client, LoginAsync(factory));

        using var update = await client.PutAsJsonAsync("/api/interactions/connections/youtube", new { apiKey = "youtube-secret", channelScope = "channel-abc" });
        update.EnsureSuccessStatusCode();
        using var response = await client.GetAsync("/api/interactions/connections");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.IsFalse(body.Contains("youtube-secret", StringComparison.Ordinal));
        Assert.IsTrue(body.Contains("configured", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task TwitchOAuthCallback_InvalidOrExpiredStateReturnsBadRequestWithoutConnection()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var invalid = await client.GetAsync("/api/interactions/connections/twitch/callback?state=unknown&code=code");
        Assert.AreEqual(HttpStatusCode.BadRequest, invalid.StatusCode);

        AddToken(client, LoginAsync(factory));
        using var connections = await client.GetAsync("/api/interactions/connections");
        var body = await connections.Content.ReadAsStringAsync();
        Assert.IsFalse(body.Contains("connected", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task TwitchDegradation_DoesNotChangeYouTubeStatus()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        AddToken(client, LoginAsync(factory));
        (await client.PutAsJsonAsync("/api/interactions/connections/youtube", new { apiKey = "youtube-secret", channelScope = "channel-abc" })).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/api/interactions/connections/twitch", new { accessToken = "twitch-secret", channelScope = "channel-abc", degrade = true })).EnsureSuccessStatusCode();

        using var response = await client.GetAsync("/api/interactions/connections");
        var body = await response.Content.ReadAsStringAsync();

        Assert.IsTrue(body.Contains("YouTube", StringComparison.Ordinal));
        Assert.IsTrue(body.Contains("configured", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(body.Contains("degraded", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task Connections_RejectStaleEtagAndPreserveMaskedSecret()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        AddToken(client, LoginAsync(factory));
        (await client.PutAsJsonAsync("/api/interactions/connections/youtube", new { apiKey = "youtube-secret", channelScope = "channel-abc" })).EnsureSuccessStatusCode();

        using var listed = await client.GetAsync("/api/interactions/connections");
        var etag = listed.Headers.ETag?.ToString();
        var listedBody = await listed.Content.ReadAsStringAsync();
        Assert.IsTrue(listedBody.Contains(ConfigSecretRedactor.UnchangedSecretMask, StringComparison.Ordinal));

        using var first = new HttpRequestMessage(HttpMethod.Put, "/api/interactions/connections/youtube")
        {
            Content = JsonContent.Create(new { apiKey = ConfigSecretRedactor.UnchangedSecretMask, channelScope = "channel-def" })
        };
        first.Headers.TryAddWithoutValidation("If-Match", etag);
        (await client.SendAsync(first)).EnsureSuccessStatusCode();

        using var stale = new HttpRequestMessage(HttpMethod.Put, "/api/interactions/connections/youtube")
        {
            Content = JsonContent.Create(new { apiKey = "replacement", channelScope = "channel-ghi" })
        };
        stale.Headers.TryAddWithoutValidation("If-Match", etag);
        using var response = await client.SendAsync(stale);
        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:EnableAdminSessionToken"] = "true",
                ["AdminAuth:SigningKey"] = "test-signing-key-for-platform-connections-1234567890"
            })));

    private static string LoginAsync(WebApplicationFactory<Program> factory)
    {
        using var client = factory.CreateClient();
        using var response = client.GetAsync("/api/test/session-token").GetAwaiter().GetResult();
        return response.Content.ReadFromJsonAsync<SessionTokenResponse>().GetAwaiter().GetResult()!.Token;
    }

    private static void AddToken(HttpClient client, string token) => client.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, token);
}
