using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EasyLotteryApi.Security;
using EasyLotteryDomain.Models.Config;
using EasyLotteryInfrastructure.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class CoreLotteryDrawHistoryApiTests
{
    [TestMethod]
    public async Task CoreDrawHistory_RequiresAdminForRosterAndRejectsObsScopedSessions()
    {
        var directory = CreateTestDirectory();
        try
        {
            await using var factory = CreateFactory(directory);
            using var anonymous = factory.CreateClient();
            var drawId = Guid.Parse("fd366ac8-0dae-4677-a3c6-515b5baf137b");

            using var deniedRead = await anonymous.GetAsync("/api/lottery-draw-records");
            Assert.AreEqual(HttpStatusCode.Unauthorized, deniedRead.StatusCode);
            using var deniedWrite = await anonymous.PostAsJsonAsync("/api/lottery-draw-records", Request(drawId, "合成觀眾", "合成獎"));
            Assert.AreEqual(HttpStatusCode.Unauthorized, deniedWrite.StatusCode);

            using var admin = factory.CreateClient();
            AddToken(admin, Login(factory));
            using var allowed = await admin.GetAsync("/api/lottery-draw-records");
            Assert.AreEqual(HttpStatusCode.OK, allowed.StatusCode);
            using var saved = await admin.PostAsJsonAsync("/api/lottery-draw-records", Request(drawId, "合成觀眾", "合成獎"));
            Assert.AreEqual(HttpStatusCode.Created, saved.StatusCode);

            using var noAdminDetail = factory.CreateClient();
            using var deniedDetail = await noAdminDetail.GetAsync($"/api/lottery-draw-records/{drawId:D}");
            Assert.AreEqual(HttpStatusCode.Unauthorized, deniedDetail.StatusCode);
            using var deniedSummary = await noAdminDetail.GetAsync($"/api/lottery-draw-records/{drawId:D}/summary");
            Assert.AreEqual(HttpStatusCode.Unauthorized, deniedSummary.StatusCode);
            using var deniedRoster = await noAdminDetail.GetAsync($"/api/lottery-draw-records/{drawId:D}/participants");
            Assert.AreEqual(HttpStatusCode.Unauthorized, deniedRoster.StatusCode);

            var obsToken = factory.Services.GetRequiredService<ObsSessionTokenService>()
                .IssueObsToken(ObsResourceKind.Donate, "synthetic-resource", [ObsSessionScope.Read]).Token;
            using var obsClient = factory.CreateClient();
            AddToken(obsClient, obsToken);
            using var obsList = await obsClient.GetAsync("/api/lottery-draw-records");
            Assert.AreEqual(HttpStatusCode.Forbidden, obsList.StatusCode);
            using var obsDetail = await obsClient.GetAsync($"/api/lottery-draw-records/{drawId:D}");
            Assert.AreEqual(HttpStatusCode.Forbidden, obsDetail.StatusCode);
            using var obsSummary = await obsClient.GetAsync($"/api/lottery-draw-records/{drawId:D}/summary");
            Assert.AreEqual(HttpStatusCode.Forbidden, obsSummary.StatusCode);
            using var obsRoster = await obsClient.GetAsync($"/api/lottery-draw-records/{drawId:D}/participants");
            Assert.AreEqual(HttpStatusCode.Forbidden, obsRoster.StatusCode);

            using var invalidWinner = await admin.PostAsJsonAsync("/api/lottery-draw-records", Request(Guid.NewGuid(), "合成觀眾", "合成獎", winnerParticipantId: "missing-ticket"));
            Assert.AreEqual(HttpStatusCode.BadRequest, invalidWinner.StatusCode);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task CoreDrawHistory_PreservesImmutableFullSnapshotButPagesMinimalSummaries()
    {
        var directory = CreateTestDirectory();
        try
        {
            await using var factory = CreateFactory(directory);
            using var client = factory.CreateClient();
            AddToken(client, Login(factory));
            var drawId = Guid.Parse("fd366ac8-0dae-4677-a3c6-515b5baf137b");
            var original = Request(drawId, "合成觀眾", "合成禮盒");

            using var firstSave = await client.PostAsJsonAsync("/api/lottery-draw-records", original);
            Assert.AreEqual(HttpStatusCode.Created, firstSave.StatusCode);
            using var firstJson = JsonDocument.Parse(await firstSave.Content.ReadAsStringAsync());
            var firstRecord = firstJson.RootElement;
            Assert.AreEqual(drawId, firstRecord.GetProperty("drawId").GetGuid());
            Assert.AreEqual("ticket-winner", firstRecord.GetProperty("winnerParticipantId").GetString());
            Assert.AreEqual("合成觀眾", firstRecord.GetProperty("winnerDisplayName").GetString());
            Assert.AreEqual("合成禮盒", firstRecord.GetProperty("prizeName").GetString());
            Assert.IsTrue(firstRecord.TryGetProperty("drawnAtUtc", out _));
            Assert.AreEqual(8, firstRecord.EnumerateObject().Count());
            var ruleSnapshot = firstRecord.GetProperty("ruleSnapshot");
            Assert.AreEqual("secure-random-index-v1", ruleSnapshot.GetProperty("algorithmVersion").GetString());
            Assert.AreEqual(2, ruleSnapshot.GetProperty("eligibleEntryCount").GetInt32());
            Assert.AreEqual("first-remaining-prize-v1", ruleSnapshot.GetProperty("prizeSelectionPolicyVersion").GetString());
            Assert.AreEqual(3, firstRecord.GetProperty("participantCount").GetInt32());
            Assert.IsFalse(firstRecord.TryGetProperty("participants", out _));
            Assert.IsFalse(firstRecord.TryGetProperty("externalIdentity", out _));
            Assert.IsFalse(firstRecord.TryGetProperty("apiKey", out _));
            Assert.IsFalse(firstRecord.TryGetProperty("password", out _));

            using var duplicate = await client.PostAsJsonAsync("/api/lottery-draw-records", original);
            Assert.AreEqual(HttpStatusCode.OK, duplicate.StatusCode);
            using var changedIdentity = await client.PostAsJsonAsync("/api/lottery-draw-records", Request(drawId, "合成觀眾", "合成禮盒", "different-synthetic-account"));
            Assert.AreEqual(HttpStatusCode.Conflict, changedIdentity.StatusCode);
            using var changedRules = await client.PostAsJsonAsync("/api/lottery-draw-records", Request(drawId, "合成觀眾", "合成禮盒", "synthetic-account-1", alteredRule: true));
            Assert.AreEqual(HttpStatusCode.Conflict, changedRules.StatusCode);

            using var reconciled = await client.GetAsync($"/api/lottery-draw-records/{drawId:D}");
            Assert.AreEqual(HttpStatusCode.OK, reconciled.StatusCode);
            using var reconciledJson = JsonDocument.Parse(await reconciled.Content.ReadAsStringAsync());
            Assert.AreEqual(3, reconciledJson.RootElement.GetProperty("participantCount").GetInt32());
            Assert.IsFalse(reconciledJson.RootElement.TryGetProperty("participants", out _));
            Assert.IsFalse(reconciledJson.RootElement.TryGetProperty("externalIdentity", out _));

            using var summary = await client.GetAsync($"/api/lottery-draw-records/{drawId:D}/summary");
            using var summaryJson = JsonDocument.Parse(await summary.Content.ReadAsStringAsync());
            Assert.AreEqual(3, summaryJson.RootElement.GetProperty("participantCount").GetInt32());
            Assert.IsFalse(summaryJson.RootElement.TryGetProperty("participants", out _));
            Assert.IsFalse(summaryJson.RootElement.TryGetProperty("externalIdentity", out _));

            using var rosterPageOne = await client.GetAsync($"/api/lottery-draw-records/{drawId:D}/participants?page=1&pageSize=2");
            using var rosterPageOneJson = JsonDocument.Parse(await rosterPageOne.Content.ReadAsStringAsync());
            Assert.AreEqual(3, rosterPageOneJson.RootElement.GetProperty("totalCount").GetInt32());
            Assert.AreEqual(2, rosterPageOneJson.RootElement.GetProperty("items").GetArrayLength());
            using var rosterPageTwo = await client.GetAsync($"/api/lottery-draw-records/{drawId:D}/participants?page=2&pageSize=2");
            using var rosterPageTwoJson = JsonDocument.Parse(await rosterPageTwo.Content.ReadAsStringAsync());
            Assert.AreEqual(1, rosterPageTwoJson.RootElement.GetProperty("items").GetArrayLength());
            using var invalidRosterPage = await client.GetAsync($"/api/lottery-draw-records/{drawId:D}/participants?pageSize=101");
            Assert.AreEqual(HttpStatusCode.BadRequest, invalidRosterPage.StatusCode);

            using var filtered = await client.GetAsync("/api/lottery-draw-records?search=%E5%90%88%E6%88%90%E8%A7%80%E7%9C%BE&page=1&pageSize=1");
            using var filteredJson = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync());
            Assert.AreEqual(1, filteredJson.RootElement.GetProperty("totalCount").GetInt32());
            var listRow = filteredJson.RootElement.GetProperty("items")[0];
            Assert.AreEqual(drawId, listRow.GetProperty("drawId").GetGuid());
            Assert.AreEqual(3, listRow.GetProperty("participantCount").GetInt32());
            Assert.IsFalse(listRow.TryGetProperty("participants", out _));
            Assert.IsFalse(listRow.TryGetProperty("externalIdentity", out _));

            using var empty = await client.GetAsync("/api/lottery-draw-records?search=不存在的結果");
            using var emptyJson = JsonDocument.Parse(await empty.Content.ReadAsStringAsync());
            Assert.AreEqual(0, emptyJson.RootElement.GetProperty("totalCount").GetInt32());
            using var notFound = await client.GetAsync($"/api/lottery-draw-records/{Guid.NewGuid():D}");
            Assert.AreEqual(HttpStatusCode.NotFound, notFound.StatusCode);

            foreach (var index in new[] { 2, 3 })
            {
                using var added = await client.PostAsJsonAsync("/api/lottery-draw-records", Request(Guid.NewGuid(), $"合成觀眾{index}", "合成禮盒"));
                Assert.AreEqual(HttpStatusCode.Created, added.StatusCode);
            }
            using var pageOne = await client.GetAsync("/api/lottery-draw-records?page=1&pageSize=2");
            using var pageOneJson = JsonDocument.Parse(await pageOne.Content.ReadAsStringAsync());
            Assert.AreEqual(3, pageOneJson.RootElement.GetProperty("totalCount").GetInt32());
            Assert.AreEqual(2, pageOneJson.RootElement.GetProperty("totalPages").GetInt32());
            Assert.AreEqual(2, pageOneJson.RootElement.GetProperty("items").GetArrayLength());
            using var pageTwo = await client.GetAsync("/api/lottery-draw-records?page=2&pageSize=2");
            using var pageTwoJson = JsonDocument.Parse(await pageTwo.Content.ReadAsStringAsync());
            Assert.AreEqual(1, pageTwoJson.RootElement.GetProperty("items").GetArrayLength());
            Assert.AreEqual(2, pageTwoJson.RootElement.GetProperty("page").GetInt32());

            var fromUtc = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-1).ToString("O"));
            var beforeUtc = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"));
            using var dateRange = await client.GetAsync($"/api/lottery-draw-records?fromUtc={fromUtc}&beforeUtc={beforeUtc}");
            using var dateRangeJson = JsonDocument.Parse(await dateRange.Content.ReadAsStringAsync());
            Assert.AreEqual(3, dateRangeJson.RootElement.GetProperty("totalCount").GetInt32());
            using var invalidDateRange = await client.GetAsync($"/api/lottery-draw-records?fromUtc={beforeUtc}&beforeUtc={fromUtc}");
            Assert.AreEqual(HttpStatusCode.BadRequest, invalidDateRange.StatusCode);
            using var invalidPageSize = await client.GetAsync("/api/lottery-draw-records?pageSize=101");
            Assert.AreEqual(HttpStatusCode.BadRequest, invalidPageSize.StatusCode);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task CoreDrawHistory_UsesSqliteStoreWithFullSnapshotAndIdempotentMutation()
    {
        var directory = CreateTestDirectory();
        try
        {
            var databasePath = Path.Combine(directory, "core-draw-history.db");
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Storage:Provider"] = "sqlite",
                    ["Storage:ConnectionString"] = $"Data Source={databasePath}",
                    ["Storage:Directory"] = directory
                })));
            using var client = factory.CreateClient();
            AddToken(client, Login(factory));
            var request = Request(Guid.Parse("87cb2fe4-a668-46e8-ae8b-f8f88bd32be4"), "SQLite 合成觀眾", "SQLite 合成獎");

            using var first = await client.PostAsJsonAsync("/api/lottery-draw-records", request);
            Assert.AreEqual(HttpStatusCode.Created, first.StatusCode);
            using var duplicate = await client.PostAsJsonAsync("/api/lottery-draw-records", request);
            Assert.AreEqual(HttpStatusCode.OK, duplicate.StatusCode);
            var stored = await factory.Services.GetRequiredService<SqliteConfigStore>().LoadAsync();
            Assert.AreEqual(1, stored.CoreLotteryDrawRecords.Count);
            Assert.AreEqual("SQLite 合成觀眾", stored.CoreLotteryDrawRecords.Single().WinnerDisplayName);
            Assert.AreEqual(3, stored.CoreLotteryDrawRecords.Single().Participants.Count);
            Assert.AreEqual("synthetic-account-1", stored.CoreLotteryDrawRecords.Single().Participants[0].ExternalIdentity?.AccountId);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static object Request(Guid id, string winner, string prize, string externalAccountId = "synthetic-account-1", bool alteredRule = false, string winnerParticipantId = "ticket-winner") => new
    {
        drawId = id,
        winnerParticipantId,
        ruleSnapshot = new
        {
            algorithmVersion = "secure-random-index-v1",
            eligibilityPolicyVersion = "not-previously-won-v1",
            groupWinnerPolicyVersion = "exclude-lottery-group-after-win-v1",
            eligibleEntryCount = 2,
            prizeSelectionPolicyVersion = "first-remaining-prize-v1",
            prizeQueue = new[] { prize, alteredRule ? "不同合成次獎" : "合成次獎" }
        },
        participants = new object[]
        {
            new
            {
                participantId = "ticket-winner", lotteryGroupId = "group-winner", displayName = winner,
                externalIdentity = new { platform = "youtube", accountId = externalAccountId }, level = "Gold", group = "Members",
                tags = new[] { "synthetic-entry" }, eligibilityStatus = "eligible"
            },
            new
            {
                participantId = "ticket-candidate-two", lotteryGroupId = "group-two", displayName = "第二合成觀眾",
                externalIdentity = new { platform = "twitch", accountId = "synthetic-account-2" }, level = "Silver", group = "Guests",
                tags = new[] { "synthetic-tag" }, eligibilityStatus = "eligible"
            },
            new
            {
                participantId = "ticket-excluded", lotteryGroupId = "group-excluded", displayName = "先前得獎合成觀眾",
                externalIdentity = (object?)null, level = "", group = "Guests", tags = new[] { "already-won" }, eligibilityStatus = "already-won"
            }
        },
        apiKey = "synthetic-api-key-should-not-persist",
        password = "synthetic-password-should-not-persist"
    };

    private static string CreateTestDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-core-draw-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static WebApplicationFactory<Program> CreateFactory(string directory) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Directory"] = directory
            })));

    private static string Login(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token;

    private static void AddToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, token);
}
