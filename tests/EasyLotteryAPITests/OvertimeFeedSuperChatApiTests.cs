using System.Net;
using System.Net.Http.Json;
using EasyLotteryApi.Security;
using EasyLotteryApplication.Payments;
using EasyLotteryDomain.Models.Overtime;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class OvertimeFeedSuperChatApiTests
{
    [TestMethod]
    public async Task SuperChatAppend_PreservesExistingEventsAndDeduplicatesAtomically()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-superchat-{Guid.NewGuid():N}");
        await using var factory = CreateFactory(directory);
        var repository = factory.Services.GetRequiredService<IOvertimeFeedRepository>();
        var existing = new OvertimeSupportEvent
        {
            Source = OvertimeSupportSource.Manual,
            DisplayName = "既有捐款",
            ExternalId = "manual-1"
        };
        await repository.SaveAsync([existing]);

        using var client = factory.CreateClient();
        var adminToken = factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token;
        var token = await IssueControlTokenAsync(client, adminToken);

        var superChat = new OvertimeSupportEvent
        {
            Source = OvertimeSupportSource.SuperChat,
            SourceLabel = "YouTube SuperChat",
            DisplayName = "Viewer",
            Message = "抽獎",
            ExternalId = "youtube-message-1"
        };
        using var first = SuperChatRequest(superChat, token);
        using var duplicate = SuperChatRequest(superChat, token);
        var responses = await Task.WhenAll(client.SendAsync(first), client.SendAsync(duplicate));
        Assert.IsTrue(responses.All(response => response.StatusCode == HttpStatusCode.NoContent));
        foreach (var response in responses) response.Dispose();

        var events = await repository.ListAsync();
        Assert.AreEqual(2, events.Count);
        Assert.IsTrue(events.Any(item => item.Id == existing.Id));
        Assert.AreEqual(1, events.Count(item => item.ExternalId == "youtube-message-1"));
    }

    [TestMethod]
    public async Task StaleFullFeedPut_PreservesSuperChatAppendedAfterItsRead()
    {
        await AssertStalePutKeepsSuperChatAsync(sqlite: false);
    }

    [TestMethod]
    public async Task SqliteStaleFullFeedPut_PreservesSuperChatAppendedAfterItsRead()
    {
        await AssertStalePutKeepsSuperChatAsync(sqlite: true);
    }

    [TestMethod]
    public async Task StaleFullFeedPut_PreservesThirdPartyPaymentAppendedAfterItsRead()
    {
        await AssertStalePutKeepsThirdPartyPaymentAsync(sqlite: false);
    }

    [TestMethod]
    public async Task SqliteStaleFullFeedPut_PreservesThirdPartyPaymentAppendedAfterItsRead()
    {
        await AssertStalePutKeepsThirdPartyPaymentAsync(sqlite: true);
    }

    [TestMethod]
    public async Task StaleFullFeedPutWithMixedPaymentSources_PreservesNewPaymentAtCapacity()
    {
        await AssertStalePutKeepsMixedPaymentsAtCapacityAsync(sqlite: false);
    }

    [TestMethod]
    public async Task SqliteStaleFullFeedPutWithMixedPaymentSources_PreservesNewPaymentAtCapacity()
    {
        await AssertStalePutKeepsMixedPaymentsAtCapacityAsync(sqlite: true);
    }

    [TestMethod]
    public async Task SuperChatAppendToFullFeed_KeepsNewEventAndEvictsOldestExistingEvent()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-superchat-full-{Guid.NewGuid():N}");
        await using var factory = CreateFactory(directory);
        var repository = factory.Services.GetRequiredService<IOvertimeFeedRepository>();
        var existing = Enumerable.Range(0, 30).Select(index => new OvertimeSupportEvent
        {
            Source = OvertimeSupportSource.Manual,
            DisplayName = $"Event {index}",
            ExternalId = $"manual-{index}",
            OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-index)
        }).ToArray();
        await repository.SaveAsync(existing);

        using var client = factory.CreateClient();
        var token = await IssueControlTokenAsync(client, factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token);
        var lateEvent = new OvertimeSupportEvent
        {
            Source = OvertimeSupportSource.SuperChat,
            DisplayName = "Late viewer",
            ExternalId = "youtube-late-message",
            OccurredAtUtc = DateTimeOffset.UtcNow.AddDays(-1)
        };
        using var request = SuperChatRequest(lateEvent, token);
        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        var saved = await repository.ListAsync();
        Assert.AreEqual(30, saved.Count);
        Assert.IsTrue(saved.Any(item => item.ExternalId == "youtube-late-message"));
        Assert.IsFalse(saved.Any(item => item.ExternalId == "manual-29"));
    }

    [TestMethod]
    public async Task StaleFullFeedPutWithNewSuperChat_KeepsIncomingAdditionWhenExistingFeedIsFull()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-superchat-put-full-{Guid.NewGuid():N}");
        await using var factory = CreateFactory(directory);
        var repository = factory.Services.GetRequiredService<IOvertimeFeedRepository>();
        var existing = Enumerable.Range(0, 30).Select(index => new OvertimeSupportEvent
        {
            Source = OvertimeSupportSource.SuperChat,
            DisplayName = $"Viewer {index}",
            ExternalId = $"youtube-existing-{index}",
            OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-index)
        }).ToArray();
        await repository.SaveAsync(existing);
        var staleSnapshot = (await repository.ListAsync()).Take(29).ToList();
        staleSnapshot.Insert(0, new OvertimeSupportEvent
        {
            Source = OvertimeSupportSource.SuperChat,
            DisplayName = "New viewer",
            ExternalId = "youtube-new-from-old-client",
            OccurredAtUtc = DateTimeOffset.UtcNow
        });

        using var client = factory.CreateClient();
        var token = await IssueControlTokenAsync(client, factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token);
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/overtime-feed") { Content = JsonContent.Create(staleSnapshot) };
        request.Headers.Add(ObsSessionTokenService.HeaderName, token);
        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        var saved = await repository.ListAsync();
        Assert.AreEqual(30, saved.Count);
        Assert.IsTrue(saved.Any(item => item.ExternalId == "youtube-new-from-old-client"));
        Assert.AreEqual(29, saved.Count(item => item.Source == OvertimeSupportSource.SuperChat && item.ExternalId != "youtube-new-from-old-client"));
    }

    [TestMethod]
    public async Task SuperChatAppend_RejectsReadOnlyOvertimeSession()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-superchat-scope-{Guid.NewGuid():N}");
        await using var factory = CreateFactory(directory);
        using var client = factory.CreateClient();
        var adminToken = factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token;
        var readOnlyToken = await IssueObsTokenAsync(client, adminToken, ["read"]);

        using var request = SuperChatRequest(new OvertimeSupportEvent
        {
            Source = OvertimeSupportSource.SuperChat,
            ExternalId = "youtube-read-only"
        }, readOnlyToken);
        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.AreEqual(0, (await factory.Services.GetRequiredService<IOvertimeFeedRepository>().ListAsync()).Count);
    }

    private static async Task AssertStalePutKeepsSuperChatAsync(bool sqlite)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-superchat-stale-{Guid.NewGuid():N}");
        await using var factory = CreateFactory(directory, sqlite);
        var repository = factory.Services.GetRequiredService<IOvertimeFeedRepository>();
        var existing = new OvertimeSupportEvent
        {
            Source = OvertimeSupportSource.Manual,
            DisplayName = "Existing",
            ExternalId = "manual-existing"
        };
        await repository.SaveAsync([existing]);
        var staleSnapshot = (await repository.ListAsync()).ToList();

        using var client = factory.CreateClient();
        var token = await IssueControlTokenAsync(client, factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token);
        using var append = SuperChatRequest(new OvertimeSupportEvent
        {
            Source = OvertimeSupportSource.SuperChat,
            DisplayName = "Viewer",
            ExternalId = "youtube-race-message"
        }, token);
        using var appended = await client.SendAsync(append);
        Assert.AreEqual(HttpStatusCode.NoContent, appended.StatusCode);

        using var stalePut = new HttpRequestMessage(HttpMethod.Put, "/api/overtime-feed") { Content = JsonContent.Create(staleSnapshot) };
        stalePut.Headers.Add(ObsSessionTokenService.HeaderName, token);
        using var saved = await client.SendAsync(stalePut);
        Assert.AreEqual(HttpStatusCode.NoContent, saved.StatusCode);

        var events = await repository.ListAsync();
        Assert.IsTrue(events.Any(item => item.ExternalId == "manual-existing"));
        Assert.IsTrue(events.Any(item => item.ExternalId == "youtube-race-message"));
    }

    private static async Task AssertStalePutKeepsThirdPartyPaymentAsync(bool sqlite)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-payment-stale-{Guid.NewGuid():N}");
        await using var factory = CreateFactory(directory, sqlite);
        var repository = factory.Services.GetRequiredService<IOvertimeFeedRepository>();
        var existing = new OvertimeSupportEvent
        {
            Source = OvertimeSupportSource.Manual,
            DisplayName = "Existing",
            ExternalId = "manual-existing"
        };
        await repository.SaveAsync([existing]);
        var staleSnapshot = (await repository.ListAsync()).ToList();

        await factory.Services.GetRequiredService<IOvertimeFeedMutationRepository>().MutateAsync(events =>
        {
            events.Add(new OvertimeSupportEvent
            {
                Source = OvertimeSupportSource.ThirdPartyPayment,
                DisplayName = "ECPay callback",
                ExternalId = "ecpay-callback-race"
            });
            return Task.CompletedTask;
        });
        staleSnapshot.Add(new OvertimeSupportEvent
        {
            Source = OvertimeSupportSource.ThirdPartyPayment,
            DisplayName = "ECPay demo",
            ExternalId = "ecpay-demo-from-old-client"
        });

        using var client = factory.CreateClient();
        var token = await IssueControlTokenAsync(client, factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token);
        using var stalePut = new HttpRequestMessage(HttpMethod.Put, "/api/overtime-feed") { Content = JsonContent.Create(staleSnapshot) };
        stalePut.Headers.Add(ObsSessionTokenService.HeaderName, token);
        using var saved = await client.SendAsync(stalePut);

        Assert.AreEqual(HttpStatusCode.NoContent, saved.StatusCode);
        var events = await repository.ListAsync();
        Assert.IsTrue(events.Any(item => item.ExternalId == "manual-existing"));
        Assert.IsTrue(events.Any(item => item.ExternalId == "ecpay-callback-race"));
        Assert.IsTrue(events.Any(item => item.ExternalId == "ecpay-demo-from-old-client"));
    }

    private static async Task AssertStalePutKeepsMixedPaymentsAtCapacityAsync(bool sqlite)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-payment-full-{Guid.NewGuid():N}");
        await using var factory = CreateFactory(directory, sqlite);
        var repository = factory.Services.GetRequiredService<IOvertimeFeedRepository>();
        var existing = Enumerable.Range(0, 30).Select(index => new OvertimeSupportEvent
        {
            Source = index == 0 ? OvertimeSupportSource.SuperChat
                : index == 1 ? OvertimeSupportSource.ThirdPartyPayment
                : OvertimeSupportSource.Manual,
            DisplayName = $"Existing {index}",
            ExternalId = $"existing-{index}",
            OccurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-index)
        }).ToArray();
        await repository.SaveAsync(existing);
        var staleSnapshot = (await repository.ListAsync()).Take(29).ToList();

        await factory.Services.GetRequiredService<IOvertimeFeedMutationRepository>().MutateAsync(events =>
        {
            events.Add(new OvertimeSupportEvent
            {
                Source = OvertimeSupportSource.ThirdPartyPayment,
                DisplayName = "Concurrent payment",
                ExternalId = "ecpay-callback-at-capacity",
                OccurredAtUtc = DateTimeOffset.UtcNow
            });
            return Task.CompletedTask;
        });
        staleSnapshot.Add(new OvertimeSupportEvent
        {
            Source = OvertimeSupportSource.ThirdPartyPayment,
            DisplayName = "Demo payment",
            ExternalId = "ecpay-demo-at-capacity",
            OccurredAtUtc = DateTimeOffset.UtcNow.AddSeconds(1)
        });

        using var client = factory.CreateClient();
        var token = await IssueControlTokenAsync(client, factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token);
        using var stalePut = new HttpRequestMessage(HttpMethod.Put, "/api/overtime-feed") { Content = JsonContent.Create(staleSnapshot) };
        stalePut.Headers.Add(ObsSessionTokenService.HeaderName, token);
        using var saved = await client.SendAsync(stalePut);

        Assert.AreEqual(HttpStatusCode.NoContent, saved.StatusCode);
        var events = await repository.ListAsync();
        Assert.AreEqual(30, events.Count);
        Assert.IsTrue(events.Any(item => item.ExternalId == "existing-0"));
        Assert.IsTrue(events.Any(item => item.ExternalId == "existing-1"));
        Assert.IsTrue(events.Any(item => item.ExternalId == "ecpay-callback-at-capacity"));
        Assert.IsTrue(events.Any(item => item.ExternalId == "ecpay-demo-at-capacity"));
    }

    private static WebApplicationFactory<Program> CreateFactory(string directory, bool sqlite = false)
    {
        Directory.CreateDirectory(directory);
        var settings = new Dictionary<string, string?> { ["Storage:Directory"] = directory };
        if (sqlite)
        {
            settings["Storage:Provider"] = "sqlite";
            settings["Storage:ConnectionString"] = $"Data Source={Path.Combine(directory, "easylottery.sqlite")}";
        }
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings)));
    }

    private static async Task<string> IssueControlTokenAsync(HttpClient client, string adminToken)
        => await IssueObsTokenAsync(client, adminToken, ["control"]);

    private static async Task<string> IssueObsTokenAsync(HttpClient client, string adminToken, string[] scopes)
    {
        using var issue = new HttpRequestMessage(HttpMethod.Post, "/api/obs-sessions")
        {
            Content = JsonContent.Create(new { resourceKind = "overtime", resourceId = "default", scopes })
        };
        issue.Headers.Add(ObsSessionTokenService.HeaderName, adminToken);
        using var issued = await client.SendAsync(issue);
        issued.EnsureSuccessStatusCode();
        return (await issued.Content.ReadFromJsonAsync<TokenResponse>())!.Token;
    }

    private static HttpRequestMessage SuperChatRequest(OvertimeSupportEvent item, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/overtime-feed/superchat") { Content = JsonContent.Create(item) };
        request.Headers.Add(ObsSessionTokenService.HeaderName, token);
        return request;
    }

    private sealed record TokenResponse(string Token, DateTimeOffset ExpiresAtUtc);
}
