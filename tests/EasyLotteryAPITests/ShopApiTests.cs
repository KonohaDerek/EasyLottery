using System.Net;
using System.Net.Http.Json;
using EasyLotteryApi.Security;
using EasyLotteryApi.Shop;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class ShopApiTests
{
    [TestMethod]
    public async Task ShopDisabled_Returns404ForApisAndShopPages()
    {
        var databasePath = CreateDatabasePath();
        await using var factory = CreateFactory(false, databasePath, _ => Task.FromResult(TokenResponse()));
        using var client = factory.CreateClient();

        foreach (var (method, path) in new[]
        {
            (HttpMethod.Get, "/api/shop/status"),
            (HttpMethod.Get, "/api/shop/hub-link"),
            (HttpMethod.Put, "/api/shop/hub-link"),
            (HttpMethod.Post, "/api/shop/hub-link/test"),
            (HttpMethod.Get, "/api/shop/products")
        })
        {
            using var response = await client.SendAsync(new HttpRequestMessage(method, path));
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, $"{method} {path}");
        }

        foreach (var path in new[] { "/shop", "/shop/packs/first", "/system/card-hub" })
        {
            using var response = await client.GetAsync(path);
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, path);
        }
    }

    [TestMethod]
    public async Task HubLinkApi_EncryptsSecretAndNeverReturnsIt()
    {
        const string secret = "hub-site-secret-one-time";
        var databasePath = CreateDatabasePath();
        await using var factory = CreateFactory(true, databasePath, _ => Task.FromResult(TokenResponse()));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, IssueAdminToken(factory));

        using var save = await client.PutAsJsonAsync("/api/shop/hub-link", new HubLinkWriteRequest
        {
            HubBaseUrl = "https://hub.example.test",
            SiteClientId = "site-client-1",
            ClientSecret = secret
        });
        Assert.AreEqual(HttpStatusCode.OK, save.StatusCode);
        var saveBody = await save.Content.ReadAsStringAsync();
        Assert.IsFalse(saveBody.Contains(secret, StringComparison.Ordinal));
        Assert.IsFalse(saveBody.Contains("encryptedSecret", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(saveBody.Contains("\"clientSecret\"", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("linked", (await save.Content.ReadFromJsonAsync<HubLinkResponse>())!.Status);

        await using var connection = new SqliteConnection($"Data Source={databasePath}");
        await connection.OpenAsync();
        await using var storedSecret = connection.CreateCommand();
        storedSecret.CommandText = "SELECT encrypted_secret FROM hub_link WHERE id = 1;";
        var cipherText = (string)(await storedSecret.ExecuteScalarAsync())!;
        Assert.IsFalse(cipherText.Contains(secret, StringComparison.Ordinal));
        Assert.IsTrue(cipherText.Length > secret.Length);

        using var read = await client.GetAsync("/api/shop/hub-link");
        Assert.AreEqual(HttpStatusCode.OK, read.StatusCode);
        var readBody = await read.Content.ReadAsStringAsync();
        Assert.IsFalse(readBody.Contains(secret, StringComparison.Ordinal));
        Assert.IsFalse(readBody.Contains("encryptedSecret", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(readBody.Contains("\"clientSecret\"", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue((await read.Content.ReadFromJsonAsync<HubLinkResponse>())!.HasClientSecret);

        using var test = await client.PostAsJsonAsync("/api/shop/hub-link/test", new HubLinkWriteRequest
        {
            HubBaseUrl = "https://hub.example.test",
            SiteClientId = "site-client-1",
            ClientSecret = secret
        });
        Assert.AreEqual(HttpStatusCode.OK, test.StatusCode);
        Assert.IsFalse((await test.Content.ReadAsStringAsync()).Contains(secret, StringComparison.Ordinal));

        using var legacy = new HttpRequestMessage(HttpMethod.Get, "/settings");
        using var legacyResponse = await client.SendAsync(legacy);
        Assert.AreEqual(HttpStatusCode.OK, legacyResponse.StatusCode);
        Assert.IsFalse((await legacyResponse.Content.ReadAsStringAsync()).Contains(secret, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task HubClient_MapsOAuthResponsesAndTransportErrors()
    {
        var linkedRequests = new List<(string Path, string Form)>();
        using (var linked = CreateFakeHubClient(request =>
        {
            linkedRequests.Add((request.RequestUri!.PathAndQuery, request.Content!.ReadAsStringAsync().GetAwaiter().GetResult()));
            return TokenResponse();
        }))
        {
            Assert.AreEqual(HubLinkStatuses.Linked, await linked.TestConnectionAsync("https://hub.example.test/", "site-id", "secret"));
        }
        Assert.AreEqual(CardHubClient.TokenEndpointPath, linkedRequests.Single().Path);
        StringAssert.Contains(linkedRequests.Single().Form, "grant_type=client_credentials");
        StringAssert.Contains(linkedRequests.Single().Form, "client_id=site-id");

        using (var invalid = CreateFakeHubClient(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { error = "invalid_client" })
        }))
        {
            Assert.AreEqual(HubLinkStatuses.CredentialsInvalid, await invalid.TestConnectionAsync("https://hub.example.test", "site-id", "bad"));
        }

        using (var unauthorized = CreateFakeHubClient(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = JsonContent.Create(new { error = "invalid_client" })
        }))
        {
            Assert.AreEqual(HubLinkStatuses.CredentialsInvalid, await unauthorized.TestConnectionAsync("https://hub.example.test", "site-id", "bad"));
        }

        using (var unavailable = CreateFakeHubClient(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)))
        {
            Assert.AreEqual(HubLinkStatuses.HubUnreachable, await unavailable.TestConnectionAsync("https://hub.example.test", "site-id", "secret"));
        }

        using (var networkFailure = CreateFakeHubClient(_ => throw new HttpRequestException("offline")))
        {
            Assert.AreEqual(HubLinkStatuses.HubUnreachable, await networkFailure.TestConnectionAsync("https://hub.example.test", "site-id", "secret"));
        }
    }

    private static CardHubClient CreateFakeHubClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory) =>
        new(new HttpClient(new FakeHandler(request => Task.FromResult(responseFactory(request)))));

    private static HttpResponseMessage TokenResponse() => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new { access_token = "test-token", token_type = "Bearer", expires_in = 60 })
    };

    private static WebApplicationFactory<Program> CreateFactory(
        bool shopEnabled,
        string databasePath,
        Func<HttpRequestMessage, Task<HttpResponseMessage>> responseFactory)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            var settingsDirectory = Path.Combine(Path.GetTempPath(), $"easy-lottery-shop-tests-{Guid.NewGuid():N}");
            var values = new Dictionary<string, string?>
            {
                ["Shop:Enabled"] = shopEnabled.ToString(),
                ["Storage:Provider"] = "sqlite",
                ["Storage:ConnectionString"] = $"Data Source={databasePath}",
                ["Storage:Directory"] = settingsDirectory
            };
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
            builder.ConfigureTestServices(services => services
                .AddHttpClient<CardHubClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new FakeHandler(responseFactory)));
        });
    }

    private static string IssueAdminToken(WebApplicationFactory<Program> factory) =>
        factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token;

    private static string CreateDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"easy-lottery-shop-{Guid.NewGuid():N}.db");

    private sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            sendAsync(request);
    }
}
