using System.Net;
using System.Net.Http.Json;
using EasyLotteryApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class SecurityEndpointTests
{
    [TestMethod]
    public async Task AdminLogin_RequiresCorrectPassword()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var denied = await client.PostAsJsonAsync("/api/admin/session", new { password = "wrong" });
        var allowed = await client.PostAsJsonAsync("/api/admin/session", new { password = "test-admin-password" });

        Assert.AreEqual(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, allowed.StatusCode);
        Assert.IsFalse(string.IsNullOrWhiteSpace((await allowed.Content.ReadFromJsonAsync<TokenResponse>())?.Token));
    }

    [TestMethod]
    public async Task ObsToken_CannotWriteSettingsOrManageTunnel()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await LoginAsync(client);
        using var issue = new HttpRequestMessage(HttpMethod.Post, "/api/obs-sessions")
        {
            Content = JsonContent.Create(new { resourceKind = "donate", resourceId = Guid.NewGuid().ToString(), scopes = new[] { "read", "control" } })
        };
        issue.Headers.Add(ObsSessionTokenService.HeaderName, adminToken);
        using var issued = await client.SendAsync(issue);
        var obsToken = (await issued.Content.ReadFromJsonAsync<TokenResponse>())!.Token;

        using var settings = new HttpRequestMessage(HttpMethod.Put, "/settings") { Content = new StringContent("{}") };
        settings.Headers.Add(ObsSessionTokenService.HeaderName, obsToken);
        using var settingsResult = await client.SendAsync(settings);
        using var tunnel = new HttpRequestMessage(HttpMethod.Post, "/api/tunnel/start") { Content = JsonContent.Create(new { provider = "none" }) };
        tunnel.Headers.Add(ObsSessionTokenService.HeaderName, obsToken);
        using var tunnelResult = await client.SendAsync(tunnel);

        Assert.AreEqual(HttpStatusCode.Forbidden, settingsResult.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, tunnelResult.StatusCode);
    }

    [TestMethod]
    public async Task ObsToken_ReadsOnlyItsBoundResourceWithoutSecrets()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await LoginAsync(client);
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var document = new EasyLotteryConfigDocument
        {
            DonateLotteryActivities =
            [
                new DonateLotteryActivity { Id = 1, PublicId = firstId, Name = "Scoped Activity" },
                new DonateLotteryActivity { Id = 2, PublicId = secondId, Name = "Other Activity" }
            ]
        };
        document.SystemSettings.MailDelivery.SmtpPassword = "must-not-leak";
        var yaml = YamlSerialization.CreateSerializerBuilder().Build().Serialize(document);
        using var save = new HttpRequestMessage(HttpMethod.Put, "/settings") { Content = new StringContent(yaml) };
        save.Headers.Add(ObsSessionTokenService.HeaderName, adminToken);
        using var saved = await client.SendAsync(save);
        saved.EnsureSuccessStatusCode();
        var obsToken = await IssueObsTokenAsync(client, adminToken, "donate", firstId.ToString(), ["read"]);

        using var read = new HttpRequestMessage(HttpMethod.Get, "/settings");
        read.Headers.Add(ObsSessionTokenService.HeaderName, obsToken);
        using var response = await client.SendAsync(read);
        var projectedYaml = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(projectedYaml, "Scoped Activity");
        Assert.IsFalse(projectedYaml.Contains("Other Activity", StringComparison.Ordinal));
        Assert.IsFalse(projectedYaml.Contains("must-not-leak", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task LoginRateLimit_ReturnsTooManyRequests()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        HttpResponseMessage? response = null;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            response?.Dispose();
            response = await client.PostAsJsonAsync("/api/admin/session", new { password = "wrong" });
        }

        using (response)
            Assert.AreEqual(HttpStatusCode.TooManyRequests, response!.StatusCode);
    }

    [TestMethod]
    public async Task OversizedRequest_IsRejected()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var content = new ByteArrayContent(new byte[1_048_577]);
        using var response = await client.PostAsync("/api/admin/session", content);

        Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:AdminPassword"] = "test-admin-password",
                ["Storage:Directory"] = Path.Combine(Path.GetTempPath(), $"easy-lottery-security-tests-{Guid.NewGuid():N}")
            })));

    private static async Task<string> LoginAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/admin/session", new { password = "test-admin-password" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.Token;
    }

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
