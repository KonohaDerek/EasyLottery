using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EasyLotteryApi.Security;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class DonateDrawHistoryApiTests
{
    [TestMethod]
    public async Task DonateHistory_RequiresAdminAndRejectsObsScopedSessions()
    {
        var directory = CreateTestDirectory();
        try
        {
            await using var factory = CreateFactory(directory);
            await SeedRecordsAsync(factory);
            using var anonymous = factory.CreateClient();

            using var denied = await anonymous.GetAsync("/api/donate-lottery-draw-records");
            Assert.AreEqual(HttpStatusCode.Unauthorized, denied.StatusCode);

            using var admin = factory.CreateClient();
            var adminToken = LoginAsync(factory);
            AddToken(admin, adminToken);
            using var allowed = await admin.GetAsync("/api/donate-lottery-draw-records");
            Assert.AreEqual(HttpStatusCode.OK, allowed.StatusCode);

            var activity = (await admin.GetFromJsonAsync<DonateLotteryActivity[]>("/api/donate-activities"))!.Single();
            var obsToken = await IssueObsTokenAsync(admin, adminToken, activity.PublicId.ToString(), ["read"]);
            using var obsRequest = new HttpRequestMessage(HttpMethod.Get, "/api/donate-lottery-draw-records");
            obsRequest.Headers.Add(ObsSessionTokenService.HeaderName, obsToken);
            using var obsDenied = await admin.SendAsync(obsRequest);
            Assert.AreEqual(HttpStatusCode.Forbidden, obsDenied.StatusCode);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task DonateHistory_FiltersPagesAndOmitsPaymentDetails()
    {
        var directory = CreateTestDirectory();
        try
        {
            await using var factory = CreateFactory(directory);
            await SeedRecordsAsync(factory);
            using var client = factory.CreateClient();
            AddToken(client, LoginAsync(factory));

            var filteredQuery = "/api/donate-lottery-draw-records?fromUtc=2026-10-02T00%3A00%3A00Z"
                + "&beforeUtc=2026-10-04T00%3A00%3A00Z&activityId=7&outcome=winning"
                + "&search=%E7%89%B9%E7%8D%8E&page=1&pageSize=1";
            using var filteredResponse = await client.GetAsync(filteredQuery);
            Assert.AreEqual(HttpStatusCode.OK, filteredResponse.StatusCode);
            using var filteredJson = JsonDocument.Parse(await filteredResponse.Content.ReadAsStringAsync());
            var filteredRoot = filteredJson.RootElement;
            Assert.AreEqual(1, filteredRoot.GetProperty("totalCount").GetInt32());
            Assert.AreEqual(1, filteredRoot.GetProperty("totalPages").GetInt32());
            var filteredRecord = filteredRoot.GetProperty("items").EnumerateArray().Single();
            Assert.AreEqual(10, filteredRecord.GetProperty("id").GetInt32());
            Assert.AreEqual("週末 Donate 活動", filteredRecord.GetProperty("activityName").GetString());
            Assert.IsFalse(filteredRecord.TryGetProperty("paymentExternalId", out _));
            Assert.IsFalse(filteredRecord.TryGetProperty("donationMessage", out _));
            Assert.IsFalse(filteredRecord.TryGetProperty("paymentMethod", out _));

            using var secondPage = await client.GetAsync("/api/donate-lottery-draw-records?page=2&pageSize=2");
            using var secondPageJson = JsonDocument.Parse(await secondPage.Content.ReadAsStringAsync());
            var secondPageIds = secondPageJson.RootElement.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty("id").GetInt32()).ToArray();
            CollectionAssert.AreEqual(new[] { 11, 12 }, secondPageIds);
            Assert.AreEqual(2, secondPageJson.RootElement.GetProperty("page").GetInt32());

            using var deletedActivity = await client.GetAsync("/api/donate-lottery-draw-records?activityId=77");
            using var deletedJson = JsonDocument.Parse(await deletedActivity.Content.ReadAsStringAsync());
            var deletedRecord = deletedJson.RootElement.GetProperty("items").EnumerateArray().Single();
            Assert.AreEqual("已刪除活動 #77", deletedRecord.GetProperty("activityName").GetString());

            using var invalidPageSize = await client.GetAsync("/api/donate-lottery-draw-records?pageSize=101");
            Assert.AreEqual(HttpStatusCode.BadRequest, invalidPageSize.StatusCode);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task SeedRecordsAsync(WebApplicationFactory<Program> factory)
    {
        var store = factory.Services.GetRequiredService<IEasyLotteryConfigStore>();
        var document = await store.LoadAsync();
        document.DonateLotteryActivities.Add(new DonateLotteryActivity
        {
            Id = 7,
            PublicId = Guid.Parse("7b6e3891-53c3-4c6f-8433-75bdbb9b7f38"),
            Name = "週末 Donate 活動"
        });
        document.DonateLotteryDrawRecords.AddRange(
        [
            new DonateLotteryDrawRecord { Id = 10, ActivityId = 7, DrawnAtUtc = Utc(3, 0, 15), DonorName = "測試觀眾一", Amount = 200, DrawCount = 2, PrizeName = "特獎", IsWinning = true, PaymentExternalId = "private-payment-id", DonationMessage = "private message", PaymentMethod = "private-method" },
            new DonateLotteryDrawRecord { Id = 11, ActivityId = 7, DrawnAtUtc = Utc(2, 11, 0), DonorName = "測試觀眾二", Amount = 100, DrawCount = 1, PrizeName = "銘謝惠顧", IsWinning = false },
            new DonateLotteryDrawRecord { Id = 12, ActivityId = 7, DrawnAtUtc = Utc(1, 9, 0), DonorName = "測試觀眾三", Amount = 100, DrawCount = 1, PrizeName = "小獎", IsWinning = true },
            new DonateLotteryDrawRecord { Id = 13, ActivityId = 77, DrawnAtUtc = Utc(2, 12, 0), DonorName = "測試觀眾四", Amount = 300, DrawCount = 3, PrizeName = "特獎", IsWinning = true }
        ]);
        await store.SaveAsync(document);
    }

    private static DateTimeOffset Utc(int day, int hour, int minute) =>
        new(2026, 10, day, hour, minute, 0, TimeSpan.Zero);

    private static string CreateTestDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-donate-history-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static WebApplicationFactory<Program> CreateFactory(string directory) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Directory"] = directory
            })));

    private static string LoginAsync(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token;

    private static void AddToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, token);

    private static async Task<string> IssueObsTokenAsync(HttpClient client, string adminToken, string resourceId, string[] scopes)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/obs-sessions")
        {
            Content = JsonContent.Create(new { resourceKind = "donate", resourceId, scopes })
        };
        request.Headers.Add(ObsSessionTokenService.HeaderName, adminToken);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        using var tokenJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return tokenJson.RootElement.GetProperty("token").GetString()!;
    }
}
