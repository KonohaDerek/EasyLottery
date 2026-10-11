using System.Net;
using System.Net.Http.Json;
using EasyLotteryApi.Lottery;
using EasyLotteryApi.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class LotteryYouTubeEndpointTests
{
    [TestMethod]
    public async Task SpaFallbacks_RouteRootToVueAndPreserveBlazorLegacyPage()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        using var vue = await client.GetAsync("/");
        Assert.AreEqual(HttpStatusCode.OK, vue.StatusCode);
        StringAssert.Contains(await vue.Content.ReadAsStringAsync(), "/vue/assets/");

        using var legacy = await client.GetAsync("/legacy");
        Assert.AreEqual(HttpStatusCode.OK, legacy.StatusCode);
        StringAssert.Contains(await legacy.Content.ReadAsStringAsync(), "blazor.webassembly.js");
    }

    [TestMethod]
    public async Task YouTubeEndpoints_RequireAdminAndExposeOnlyProjectedData()
    {
        var fake = new FakeLotteryYouTubeClient
        {
            IsConfigured = true,
            ChatId = "chat-123",
            Messages =
            [
                new LotteryYouTubeMessage(
                    "message-1", "SuperChatEvent", "Viewer", "抽獎", "2026-10-11T00:00:00Z", "https://example.test/avatar.png",
                    new LotterySuperChatDetails("5000000", "NT$5", "TWD", "支持抽獎"))
            ]
        };
        await using var factory = CreateFactory(fake);
        using var anonymous = factory.CreateClient();
        using var denied = await anonymous.GetAsync("/api/lottery/youtube/status");
        Assert.AreEqual(HttpStatusCode.Unauthorized, denied.StatusCode);

        using var client = factory.CreateClient();
        AddToken(client, factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token);

        using var status = await client.GetAsync("/api/lottery/youtube/status");
        Assert.AreEqual(HttpStatusCode.OK, status.StatusCode);
        var statusPayload = await status.Content.ReadAsStringAsync();
        StringAssert.Contains(statusPayload, "configured");
        Assert.IsFalse(statusPayload.Contains("apiKey", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(statusPayload.Contains("super-secret", StringComparison.Ordinal));

        using var invalidUrl = await client.PostAsJsonAsync("/api/lottery/youtube/live-chat/resolve", new { url = "https://attacker.example/watch?v=abc" });
        Assert.AreEqual(HttpStatusCode.BadRequest, invalidUrl.StatusCode);
        Assert.AreEqual(0, fake.ResolveCalls);

        using var resolved = await client.PostAsJsonAsync("/api/lottery/youtube/live-chat/resolve", new { url = "https://www.youtube.com/watch?v=video-1" });
        Assert.AreEqual(HttpStatusCode.OK, resolved.StatusCode);
        Assert.AreEqual("chat-123", (await resolved.Content.ReadFromJsonAsync<ResolveYouTubeLiveChatResponse>())!.ChatId);

        using var invalidChat = await client.GetAsync("/api/lottery/youtube/live-chat/%2Fprivate/messages");
        Assert.AreEqual(HttpStatusCode.BadRequest, invalidChat.StatusCode);

        using var messages = await client.GetAsync("/api/lottery/youtube/live-chat/chat-123/messages");
        Assert.AreEqual(HttpStatusCode.OK, messages.StatusCode);
        var payload = await messages.Content.ReadAsStringAsync();
        StringAssert.Contains(payload, "SuperChatEvent");
        StringAssert.Contains(payload, "5000000");
        Assert.IsFalse(payload.Contains("apiKey", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(payload.Contains("super-secret", StringComparison.Ordinal));
    }

    private static WebApplicationFactory<Program> CreateFactory(FakeLotteryYouTubeClient fake) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILotteryYouTubeClient>();
            services.AddSingleton<ILotteryYouTubeClient>(fake);
        }));

    private static void AddToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, token);

    private sealed class FakeLotteryYouTubeClient : ILotteryYouTubeClient
    {
        public bool IsConfigured { get; init; }
        public string? ChatId { get; init; }
        public IReadOnlyList<LotteryYouTubeMessage> Messages { get; init; } = [];
        public int ResolveCalls { get; private set; }

        public Task<bool> HasConfiguredCredentialsAsync(CancellationToken cancellationToken)
            => Task.FromResult(IsConfigured);

        public Task<string?> ResolveLiveChatIdAsync(string liveUrl, CancellationToken cancellationToken)
        {
            ResolveCalls++;
            return Task.FromResult(ChatId);
        }

        public Task<IReadOnlyList<LotteryYouTubeMessage>> ListMessagesAsync(string chatId, CancellationToken cancellationToken)
            => Task.FromResult(Messages);
    }
}
