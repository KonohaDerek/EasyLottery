using System.Net;
using System.Net.Http.Json;
using EasyLotteryApi;
using EasyLotteryDomain.Models.Config;
using EasyLotteryInfrastructure.Settings;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class SettingsResourceApiTests
{
    [TestMethod]
    public async Task SettingsResources_ReadAndWriteOneSectionWithEtag()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        AddToken(client, await LoginAsync(client));

        using var read = await client.GetAsync("/api/settings/obs-layout");
        Assert.AreEqual(HttpStatusCode.OK, read.StatusCode);
        var originalTag = read.Headers.ETag?.Tag;
        Assert.IsFalse(string.IsNullOrWhiteSpace(originalTag));

        using var update = new HttpRequestMessage(HttpMethod.Put, "/api/settings/obs-layout")
        {
            Content = JsonContent.Create(new ObsLayoutSettings { ActiveLayoutKey = "compact-stage" })
        };
        update.Headers.TryAddWithoutValidation("If-Match", originalTag);
        using var saved = await client.SendAsync(update);

        Assert.AreEqual(HttpStatusCode.OK, saved.StatusCode);
        Assert.AreNotEqual(originalTag, saved.Headers.ETag?.Tag);
        var value = await saved.Content.ReadFromJsonAsync<ObsLayoutSettings>();
        Assert.AreEqual("compact-stage", value!.ActiveLayoutKey);
    }

    [TestMethod]
    public async Task SettingsResources_RejectStaleEtagWithoutOverwritingChanges()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        AddToken(client, await LoginAsync(client));

        using var initial = await client.GetAsync("/api/settings/sound-cues");
        var tag = initial.Headers.ETag!.Tag;
        using var first = new HttpRequestMessage(HttpMethod.Put, "/api/settings/sound-cues")
        {
            Content = JsonContent.Create(new SoundCueSettings { ActivePresetKey = "first-stage" })
        };
        first.Headers.TryAddWithoutValidation("If-Match", tag);
        using var firstResponse = await client.SendAsync(first);
        firstResponse.EnsureSuccessStatusCode();

        using var stale = new HttpRequestMessage(HttpMethod.Put, "/api/settings/sound-cues")
        {
            Content = JsonContent.Create(new SoundCueSettings { ActivePresetKey = "retro-stage" })
        };
        stale.Headers.TryAddWithoutValidation("If-Match", tag);
        using var staleResponse = await client.SendAsync(stale);
        Assert.AreEqual(HttpStatusCode.Conflict, staleResponse.StatusCode);

        using var verify = await client.GetAsync("/api/settings/sound-cues");
        var current = await verify.Content.ReadFromJsonAsync<SoundCueSettings>();
        Assert.AreEqual("first-stage", current!.ActivePresetKey);
    }

    [TestMethod]
    public async Task SettingsResources_UseSqliteProvider()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"easy-lottery-settings-{Guid.NewGuid():N}.db");
        await using var factory = CreateSqliteFactory(databasePath);
        using var client = factory.CreateClient();
        AddToken(client, await LoginAsync(client));

        using var read = await client.GetAsync("/api/settings/obs-layout");
        var tag = read.Headers.ETag!.Tag;
        using var update = new HttpRequestMessage(HttpMethod.Put, "/api/settings/obs-layout")
        {
            Content = JsonContent.Create(new ObsLayoutSettings { ActiveLayoutKey = "sqlite-stage" })
        };
        update.Headers.TryAddWithoutValidation("If-Match", tag);
        using var response = await client.SendAsync(update);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var store = factory.Services.GetRequiredService<SqliteConfigStore>();
        var document = await store.LoadAsync();
        Assert.AreEqual("sqlite-stage", document.ObsLayout.ActiveLayoutKey);

        using var legacyUpdate = new HttpRequestMessage(HttpMethod.Put, "/settings")
        {
            Content = new StringContent("obsLayout:\n  activeLayoutKey: legacy-sqlite-stage\n")
        };
        using var legacyResponse = await client.SendAsync(legacyUpdate);
        legacyResponse.EnsureSuccessStatusCode();
        document = await store.LoadAsync();
        Assert.AreEqual("legacy-sqlite-stage", document.ObsLayout.ActiveLayoutKey);
    }

    [TestMethod]
    public async Task StorageImport_UpdatesSqliteDonateRepository()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-storage-import-{Guid.NewGuid():N}");
        var databasePath = Path.Combine(directory, "easylottery.sqlite");
        Directory.CreateDirectory(directory);
        await using var factory = CreateSqliteFactory(databasePath, directory);
        var yamlStore = factory.Services.GetRequiredService<SettingsFileStore>();
        var document = await yamlStore.ReadAsync(CancellationToken.None);
        document.DonateLotteryActivities.Add(new DonateLotteryActivity
        {
            Id = 41,
            Name = "匯入測試",
            Prizes = [new DonateLotteryPrize { Id = 1, Name = "獎項", RemainingQuantity = 4 }]
        });
        await yamlStore.SaveAsync(document);

        using var client = factory.CreateClient();
        AddToken(client, await LoginAsync(client));
        using var import = await client.PostAsync("/api/storage/import-yaml", content: null);
        Assert.AreEqual(HttpStatusCode.OK, import.StatusCode);

        using var list = await client.GetAsync("/api/donate-activities");
        var activities = await list.Content.ReadFromJsonAsync<List<DonateLotteryActivity>>();
        Assert.AreEqual(1, activities!.Count);
        Assert.AreEqual(4, activities[0].Prizes.Single().RemainingQuantity);
    }

    [TestMethod]
    public async Task DonateCrud_UsesSqliteConfigStoreAndRepository()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"easy-lottery-donate-{Guid.NewGuid():N}.db");
        await using var factory = CreateSqliteFactory(databasePath);
        using var client = factory.CreateClient();
        AddToken(client, await LoginAsync(client));

        using var create = await client.PostAsJsonAsync("/api/donate-activities", new DonateLotteryActivity
        {
            Name = "SQLite Donate",
            Prizes = [new DonateLotteryPrize { Name = "獎項", Quantity = 2, RemainingQuantity = 2 }]
        });
        Assert.AreEqual(HttpStatusCode.Created, create.StatusCode);

        using var list = await client.GetAsync("/api/donate-activities");
        var activities = await list.Content.ReadFromJsonAsync<List<DonateLotteryActivity>>();
        Assert.AreEqual(1, activities!.Count);
        var store = factory.Services.GetRequiredService<SqliteConfigStore>();
        var document = await store.LoadAsync();
        Assert.AreEqual("SQLite Donate", document.DonateLotteryActivities.Single().Name);
    }

    [TestMethod]
    public async Task PaymentSettings_RedactsSecretsAndPreservesMaskedValues()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var token = await LoginAsync(client);
        AddToken(client, token);

        using var seed = new HttpRequestMessage(HttpMethod.Put, "/settings")
        {
            Content = new StringContent("systemSettings:\n  donationIntegration:\n    ecpay:\n      name: 綠界\n      testing:\n        apiKey: keep-me\n")
        };
        using var seeded = await client.SendAsync(seed);
        seeded.EnsureSuccessStatusCode();

        using var read = await client.GetAsync("/api/settings/payments");
        Assert.AreEqual(HttpStatusCode.OK, read.StatusCode);
        var payments = await read.Content.ReadFromJsonAsync<PaymentSettingsResource>();
        Assert.AreEqual(ConfigSecretRedactor.UnchangedSecretMask, payments!.DonationIntegration.Ecpay.Testing.ApiKey);

        using var update = new HttpRequestMessage(HttpMethod.Put, "/api/settings/payments")
        {
            Content = JsonContent.Create(payments)
        };
        update.Headers.TryAddWithoutValidation("If-Match", read.Headers.ETag!.Tag);
        using var saved = await client.SendAsync(update);
        Assert.AreEqual(HttpStatusCode.OK, saved.StatusCode);

        using var full = new HttpRequestMessage(HttpMethod.Get, "/settings");
        using var fullResponse = await client.SendAsync(full);
        var yaml = await fullResponse.Content.ReadAsStringAsync();
        Assert.IsFalse(yaml.Contains("keep-me", StringComparison.Ordinal));
        var store = factory.Services.GetRequiredService<SettingsFileStore>();
        var persisted = await store.ReadAsync(CancellationToken.None);
        Assert.AreEqual("keep-me", persisted.SystemSettings.DonationIntegration.Ecpay.Testing.ApiKey);
    }

    [TestMethod]
    public async Task CompatibilitySettingsEndpoint_IsMarkedDeprecated()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        AddToken(client, await LoginAsync(client));

        using var response = await client.GetAsync("/settings");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("true", response.Headers.GetValues("Deprecation").Single());
        Assert.IsTrue(response.Headers.Contains("Link"));
    }

    [TestMethod]
    public async Task ObsReadCanAccessPresentationSettingsButNotPaymentSecrets()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await LoginAsync(client);
        var obsToken = await IssueObsTokenAsync(client, adminToken, "roulette", Guid.NewGuid().ToString(), ["read"]);

        using var presentation = new HttpRequestMessage(HttpMethod.Get, "/api/settings/sound-cues");
        presentation.Headers.Add(ObsSessionTokenService.HeaderName, obsToken);
        using var presentationResponse = await client.SendAsync(presentation);
        Assert.AreEqual(HttpStatusCode.OK, presentationResponse.StatusCode);

        using var payments = new HttpRequestMessage(HttpMethod.Get, "/api/settings/payments");
        payments.Headers.Add(ObsSessionTokenService.HeaderName, obsToken);
        using var paymentResponse = await client.SendAsync(payments);
        Assert.AreEqual(HttpStatusCode.Forbidden, paymentResponse.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Directory"] = Path.Combine(Path.GetTempPath(), $"easy-lottery-settings-resource-tests-{Guid.NewGuid():N}")
            })));

    private static WebApplicationFactory<Program> CreateSqliteFactory(string databasePath, string? directory = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Provider"] = "sqlite",
                ["Storage:ConnectionString"] = $"Data Source={databasePath}",
                ["Storage:Directory"] = directory ?? Path.GetDirectoryName(databasePath)!
            })));

    private static async Task<string> LoginAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/session-token");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.Token;
    }

    private static void AddToken(HttpClient client, string token) => client.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, token);

    private static async Task<string> IssueObsTokenAsync(HttpClient client, string adminToken, string kind, string resourceId, string[] scopes)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/obs-sessions")
        {
            Content = JsonContent.Create(new { resourceKind = kind, resourceId, scopes })
        };
        request.Headers.Add(ObsSessionTokenService.HeaderName, adminToken);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.Token;
    }

    private sealed record TokenResponse(string Token, DateTimeOffset ExpiresAtUtc);
}
