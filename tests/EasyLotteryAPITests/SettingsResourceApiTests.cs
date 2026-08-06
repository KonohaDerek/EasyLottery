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
