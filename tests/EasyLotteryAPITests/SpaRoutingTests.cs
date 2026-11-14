using System.Net;
using System.Text.Json;
using EasyLotteryApi.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class SpaRoutingTests
{
    [TestMethod]
    public async Task UnknownPages_GetVue404_HeadHasNoBody_AndOtherMethodsAreNotHtml()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var get = await client.GetAsync("/this-does-not-exist");
        Assert.AreEqual(HttpStatusCode.NotFound, get.StatusCode);
        Assert.AreEqual("text/html", get.Content.Headers.ContentType?.MediaType);
        StringAssert.Contains(await get.Content.ReadAsStringAsync(), "/vue/assets/");
        Assert.IsTrue(get.Headers.CacheControl?.NoCache);
        using var head = await client.SendAsync(new(HttpMethod.Head, "/this-does-not-exist"));
        Assert.AreEqual(HttpStatusCode.NotFound, head.StatusCode);
        Assert.AreEqual("text/html", head.Content.Headers.ContentType?.MediaType);
        Assert.AreEqual(0, (await head.Content.ReadAsByteArrayAsync()).Length);
        using var post = await client.PostAsync("/this-does-not-exist", null);
        Assert.AreEqual(HttpStatusCode.NotFound, post.StatusCode);
        Assert.AreEqual("application/problem+json", post.Content.Headers.ContentType?.MediaType);
    }

    [TestMethod]
    public async Task UnknownApiAndHubPaths_ReturnNonHtml404_ForEveryMethod()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        foreach (var path in new[] { "/api", "/api/missing", "/api/missing.json", "/hubs", "/hubs/missing", "/hubs/missing/negotiate" })
        foreach (var method in new[] { "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS" })
        {
            using var response = await client.SendAsync(new(new HttpMethod(method), path));
            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode, $"{method} {path}");
            Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            if (method != "HEAD")
            {
                using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.AreEqual(404, body.RootElement.GetProperty("status").GetInt32());
            }
        }
    }

    [TestMethod]
    public async Task RealApiAndSignalRRoutes_TakePrecedence()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/settings/backups");
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        foreach (var hub in new[] { "overtime", "live-draw", "interactions" })
        {
            using var negotiate = await client.PostAsync($"/hubs/{hub}/negotiate?negotiateVersion=1", null);
            Assert.AreEqual(HttpStatusCode.OK, negotiate.StatusCode);
            using var body = JsonDocument.Parse(await negotiate.Content.ReadAsStringAsync());
            Assert.IsTrue(body.RootElement.TryGetProperty("connectionToken", out _));
        }
    }

    [TestMethod]
    public async Task KnownVueAndLegacyDeepLinks_KeepTheirShells()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        foreach (var path in new[] { "/", "/about", "/login", "/privacy-policy", "/obs/donate/x", "/obs/overtime", "/system/access", "/roulette/preview/1", "/pokebox/preview/1" })
        {
            using var response = await client.GetAsync(path);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, path);
            StringAssert.Contains(await response.Content.ReadAsStringAsync(), "/vue/assets/");
        }
        foreach (var path in new[] { "/legacy", "/legacy/market", "/legacy/obs/donate/x", "/pokebox/editor/1", "/roulette/editor/1" })
        {
            using var response = await client.GetAsync(path);
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, path);
            StringAssert.Contains(await response.Content.ReadAsStringAsync(), "_framework/blazor.webassembly.js");
        }
        foreach (var path in new[] { "/missing-static-file.js", "/legacy/missing-static-file.js", "/vue/assets/missing.js" })
        {
            using var missingFile = await client.GetAsync(path);
            Assert.AreEqual(HttpStatusCode.NotFound, missingFile.StatusCode, path);
            Assert.AreNotEqual("text/html", missingFile.Content.Headers.ContentType?.MediaType);
        }
    }

    [TestMethod]
    public async Task Settings_DocumentNavigationRedirects_ButLegacyClientsKeepAuthAndYaml()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        foreach (var method in new[] { HttpMethod.Get, HttpMethod.Head })
        {
            foreach (var header in new[] { ("Sec-Fetch-Mode", "navigate"), ("Sec-Fetch-Dest", "document") })
            {
                using var request = new HttpRequestMessage(method, "/settings");
                request.Headers.Add(header.Item1, header.Item2);
                using var response = await client.SendAsync(request);
                Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
                Assert.AreEqual("/", response.Headers.Location?.OriginalString);
            }
            using var legacyRequest = new HttpRequestMessage(method, "/settings");
            legacyRequest.Headers.Add("Accept", "text/html");
            legacyRequest.Headers.Add("Sec-Fetch-Mode", "cors");
            using var legacy = await client.SendAsync(legacyRequest);
            Assert.AreEqual(HttpStatusCode.Unauthorized, legacy.StatusCode);
        }
        using var yamlNavigation = new HttpRequestMessage(HttpMethod.Get, "/easy-lottery-config.yaml");
        yamlNavigation.Headers.Add("Sec-Fetch-Mode", "navigate");
        using var yamlUnauthorized = await client.SendAsync(yamlNavigation);
        Assert.AreEqual(HttpStatusCode.Unauthorized, yamlUnauthorized.StatusCode);

        var token = factory.Services.GetRequiredService<ObsSessionTokenService>().IssueAdminToken().Token;
        client.DefaultRequestHeaders.Add(ObsSessionTokenService.HeaderName, token);
        using var authenticated = await client.GetAsync("/settings");
        Assert.AreEqual(HttpStatusCode.OK, authenticated.StatusCode);
        Assert.AreEqual("text/yaml", authenticated.Content.Headers.ContentType?.MediaType);
        Assert.IsNotNull(authenticated.Headers.ETag);
        Assert.IsTrue(authenticated.Headers.Contains("Deprecation"));
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            var values = new Dictionary<string, string?>
            {
                ["Storage:Directory"] = Path.Combine(Path.GetTempPath(), $"easy-lottery-routing-tests-{Guid.NewGuid():N}")
            };
            foreach (var setting in values) builder.UseSetting(setting.Key, setting.Value);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
        });
}
