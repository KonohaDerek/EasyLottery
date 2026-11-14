using System.Net;
using System.Net.Http.Json;
using EasyLotteryApi;
using EasyLotteryApi.CardShop;
using EasyLotteryApi.Security;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EasyLotteryApiTests.CardShop;

[TestClass]
public sealed class CardHubLinkApiTests
{
    [TestMethod]
    public async Task LinkSettingsRequireAdminAndNeverReturnSecret()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-card-hub-tests-{Guid.NewGuid():N}");
        await using var factory = CreateFactory(directory, CardHubConnectionState.Linked);
        using var client = factory.CreateClient();

        using var unauthenticated = await client.GetAsync("/api/settings/card-hub");
        Assert.AreEqual(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        var token = factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token;
        client.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, token);
        var initial = await client.GetFromJsonAsync<CardHubLinkResponse>("/api/settings/card-hub");
        Assert.AreEqual("not_linked", initial!.Status);
        Assert.IsFalse(initial.HasSecret);

        const string secret = "never-return-this-secret";
        using var save = await client.PutAsJsonAsync("/api/settings/card-hub", new CardHubLinkUpdate("http://127.0.0.1:22398", "site-client-1", secret));
        Assert.AreEqual(HttpStatusCode.OK, save.StatusCode);
        var body = await save.Content.ReadAsStringAsync();
        Assert.IsFalse(body.Contains(secret, StringComparison.Ordinal));
        var saved = await save.Content.ReadFromJsonAsync<CardHubLinkResponse>();
        Assert.AreEqual("linked", saved!.Status);
        Assert.IsTrue(saved.HasSecret);

        using var database = new SqliteConnection($"Data Source={Path.Combine(directory, "card-hub.sqlite")}");
        await database.OpenAsync();
        using var command = database.CreateCommand();
        command.CommandText = "SELECT encrypted_secret FROM hub_link WHERE id = 1;";
        var protectedSecret = (string)(await command.ExecuteScalarAsync())!;
        Assert.AreNotEqual(secret, protectedSecret);
        Assert.IsFalse(protectedSecret.Contains(secret, StringComparison.Ordinal));

        var reloaded = await client.GetFromJsonAsync<CardHubLinkResponse>("/api/settings/card-hub");
        Assert.IsTrue(reloaded!.HasSecret);
    }

    [TestMethod]
    public async Task SaveClassifiesInvalidCredentialsAndTestUsesStoredSecret()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-card-hub-tests-{Guid.NewGuid():N}");
        await using var factory = CreateFactory(directory, CardHubConnectionState.CredentialsInvalid, trustedOrigins: ["https://hub.example.test"]);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token);

        using var save = await client.PutAsJsonAsync("/api/settings/card-hub", new CardHubLinkUpdate("https://hub.example.test", "site-client-1", "test-secret"));
        Assert.AreEqual(HttpStatusCode.OK, save.StatusCode);
        Assert.AreEqual("credentials_invalid", (await save.Content.ReadFromJsonAsync<CardHubLinkResponse>())!.Status);

        using var test = await client.PostAsync("/api/settings/card-hub/test", content: null);
        Assert.AreEqual(HttpStatusCode.OK, test.StatusCode);
        Assert.AreEqual("credentials_invalid", (await test.Content.ReadFromJsonAsync<CardHubLinkResponse>())!.Status);
    }

    [TestMethod]
    public async Task SaveRejectsNonHttpsHubUrlOutsideLoopback()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-card-hub-tests-{Guid.NewGuid():N}");
        await using var factory = CreateFactory(directory, CardHubConnectionState.Linked);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token);

        using var response = await client.PutAsJsonAsync("/api/settings/card-hub", new CardHubLinkUpdate("http://hub.example.test", "site-client-1", "test-secret"));

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadAsStringAsync();
        Assert.IsFalse(error.Contains("test-secret", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task UntrustedHttpsOriginIsRejectedBeforeVerifierReceivesSecret()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-card-hub-tests-{Guid.NewGuid():N}");
        await using var factory = CreateFactory(directory, CardHubConnectionState.Linked);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token);
        using var save = await client.PutAsJsonAsync("/api/settings/card-hub", new CardHubLinkUpdate("https://untrusted.example.test", "site-client-1", "must-not-be-forwarded"));
        Assert.AreEqual(HttpStatusCode.BadRequest, save.StatusCode);
        var verifier = factory.Services.GetRequiredService<FakeConnectionVerifier>();
        Assert.AreEqual(0, verifier.CallCount);
        Assert.IsNull(verifier.LastSecret);
    }

    [TestMethod]
    public async Task TrustedHttpsOriginIsAccepted()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-card-hub-tests-{Guid.NewGuid():N}");
        await using var factory = CreateFactory(directory, CardHubConnectionState.Linked, trustedOrigins: ["https://hub.example.test"]);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token);
        using var save = await client.PutAsJsonAsync("/api/settings/card-hub", new CardHubLinkUpdate("https://hub.example.test/", "site-client-1", "trusted-secret"));
        Assert.AreEqual(HttpStatusCode.OK, save.StatusCode);
        var verifier = factory.Services.GetRequiredService<FakeConnectionVerifier>();
        Assert.AreEqual(1, verifier.CallCount);
        Assert.AreEqual("trusted-secret", verifier.LastSecret);
        Assert.AreEqual("https://hub.example.test", (await save.Content.ReadFromJsonAsync<CardHubLinkResponse>())!.HubUrl);
    }

    [TestMethod]
    public async Task LoopbackHttpIsAllowedOnlyInDevelopment()
    {
        var developmentDirectory = Path.Combine(Path.GetTempPath(), $"easy-lottery-card-hub-tests-{Guid.NewGuid():N}");
        await using var development = CreateFactory(developmentDirectory, CardHubConnectionState.Linked);
        using var developmentClient = development.CreateClient();
        developmentClient.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, development.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token);
        using var allowed = await developmentClient.PutAsJsonAsync("/api/settings/card-hub", new CardHubLinkUpdate("http://127.0.0.1:22398", "site-client-1", "local-secret"));
        Assert.AreEqual(HttpStatusCode.OK, allowed.StatusCode);

        var productionDirectory = Path.Combine(Path.GetTempPath(), $"easy-lottery-card-hub-tests-{Guid.NewGuid():N}");
        await using var production = CreateFactory(productionDirectory, CardHubConnectionState.Linked, environment: "Production");
        using var productionClient = production.CreateClient();
        productionClient.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, production.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token);
        using var denied = await productionClient.PutAsJsonAsync("/api/settings/card-hub", new CardHubLinkUpdate("http://127.0.0.1:22398", "site-client-1", "local-secret"));
        Assert.AreEqual(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.AreEqual(0, production.Services.GetRequiredService<FakeConnectionVerifier>().CallCount);
    }

    [TestMethod]
    public async Task VerifierRejectsUntrustedOriginWithoutSendingRequest()
    {
        var policy = new CardHubOriginPolicy(new ConfigurationBuilder().Build(), new TestHostEnvironment(".") { EnvironmentName = "Development" });
        var handler = new CountingHandler();
        var verifier = new CardHubConnectionVerifier(new HttpClient(handler), policy);
        var result = await verifier.VerifyAsync("https://untrusted.example.test", "client", "must-not-be-sent", CancellationToken.None);
        Assert.AreEqual(CardHubConnectionState.Unreachable, result);
        Assert.AreEqual(0, handler.CallCount);
    }

    [TestMethod]
    public async Task VerifierPostsClientCredentialsToTheTrustedTokenEndpoint()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Shop:TrustedHubOrigins:0"] = "https://hub.example.test"
        }).Build();
        var policy = new CardHubOriginPolicy(config, new TestHostEnvironment(".") { EnvironmentName = "Production" });
        var handler = new CountingHandler();
        var verifier = new CardHubConnectionVerifier(new HttpClient(handler), policy);

        var result = await verifier.VerifyAsync("https://hub.example.test", "test-client-id", "test-client-secret", CancellationToken.None);

        Assert.AreEqual(CardHubConnectionState.Linked, result);
        Assert.AreEqual(1, handler.CallCount);
        Assert.AreEqual(HttpMethod.Post, handler.LastMethod);
        Assert.AreEqual("https://hub.example.test/hub/v1/sites/token", handler.LastRequestUri?.ToString());
        Assert.AreEqual("application/x-www-form-urlencoded", handler.LastContentType);
        StringAssert.Contains(handler.LastBody!, "grant_type=client_credentials");
        StringAssert.Contains(handler.LastBody!, "client_id=test-client-id");
        StringAssert.Contains(handler.LastBody!, "client_secret=test-client-secret");
    }

    [TestMethod]
    public async Task TestEndpointRejectsPreviouslySavedOriginBeforeDecryptingOrVerifying()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"easy-lottery-card-hub-tests-{Guid.NewGuid():N}");
        await using var factory = CreateFactory(directory, CardHubConnectionState.Linked, trustedOrigins: ["https://hub.example.test"]);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token);
        using var save = await client.PutAsJsonAsync("/api/settings/card-hub", new CardHubLinkUpdate("https://hub.example.test", "site-client-1", "test-secret"));
        Assert.AreEqual(HttpStatusCode.OK, save.StatusCode);
        var verifier = factory.Services.GetRequiredService<FakeConnectionVerifier>();
        Assert.AreEqual(1, verifier.CallCount);

        using (var database = new SqliteConnection($"Data Source={Path.Combine(directory, "card-hub.sqlite")}"))
        {
            await database.OpenAsync();
            using var command = database.CreateCommand();
            command.CommandText = "UPDATE hub_link SET hub_url = 'https://untrusted.example.test' WHERE id = 1;";
            await command.ExecuteNonQueryAsync();
        }

        using var test = await client.PostAsync("/api/settings/card-hub/test", content: null);
        Assert.AreEqual(HttpStatusCode.BadRequest, test.StatusCode);
        Assert.AreEqual(1, verifier.CallCount);
        Assert.AreEqual("test-secret", verifier.LastSecret);
    }

    private static WebApplicationFactory<Program> CreateFactory(string directory, CardHubConnectionState state, string[]? trustedOrigins = null, string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:Directory"] = directory,
                ["Shop:Enabled"] = "false",
                ["Shop:TrustedHubOrigins:0"] = trustedOrigins?.ElementAtOrDefault(0),
                ["Shop:TrustedHubOrigins:1"] = trustedOrigins?.ElementAtOrDefault(1)
            }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICardHubConnectionVerifier>();
                services.AddSingleton(new FakeConnectionVerifier(state));
                services.AddSingleton<ICardHubConnectionVerifier>(sp => sp.GetRequiredService<FakeConnectionVerifier>());
            });
        });

    private sealed class FakeConnectionVerifier(CardHubConnectionState state) : ICardHubConnectionVerifier
    {
        public int CallCount { get; private set; }
        public string? LastSecret { get; private set; }
        public Task<CardHubConnectionState> VerifyAsync(string hubUrl, string clientId, string clientSecret, CancellationToken cancellationToken)
        {
            CallCount++;
            LastSecret = clientSecret;
            return Task.FromResult(state);
        }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public Uri? LastRequestUri { get; private set; }
        public string? LastBody { get; private set; }
        public string? LastContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastMethod = request.Method;
            LastRequestUri = request.RequestUri;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            LastContentType = request.Content?.Headers.ContentType?.ToString();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
