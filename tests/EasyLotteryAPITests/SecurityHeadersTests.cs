using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EasyLotteryApi.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class SecurityHeadersTests
{
    [TestMethod]
    public async Task Responses_IncludeProductionSecurityHeaders()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Security:ForceHttps"] = "false" })));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/health/live");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.AreEqual("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.AreEqual("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.IsTrue(response.Headers.GetValues("Content-Security-Policy").Single().Contains("frame-ancestors 'none'", StringComparison.Ordinal));
        Assert.IsTrue(response.Headers.Contains("Permissions-Policy"));
    }

    [TestMethod]
    public async Task ContentSecurityPolicy_AllowsOnlyHashedInlineScriptsInTheAppShell()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Security:ForceHttps"] = "false" })));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();
        var policy = response.Headers.GetValues("Content-Security-Policy").Single();
        var scriptPolicy = policy.Split(';').Single(part => part.TrimStart().StartsWith("script-src ", StringComparison.Ordinal));
        var inlineScripts = Regex.Matches(html, @"<script\b(?![^>]*\bsrc\s*=)[^>]*>(.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsTrue(inlineScripts.Count > 0, "The app shell must contain scripts covered by the CSP.");
        Assert.IsFalse(scriptPolicy.Contains("'unsafe-inline'", StringComparison.Ordinal));
        foreach (Match script in inlineScripts)
        {
            var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script.Groups[1].Value)));
            Assert.IsTrue(scriptPolicy.Contains($"'sha256-{hash}'", StringComparison.Ordinal), "An inline app-shell script is missing from the CSP hash allowlist.");
        }
    }

    [TestMethod]
    public async Task TrustedForwardedHttps_DoesNotRedirect_AndHttpResponsesKeepSecurityHeaders()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            var values = new Dictionary<string, string?>
            {
                ["Security:ForceHttps"] = "true",
                ["Security:AdminToken:TrustedProxyIps:0"] = "127.0.0.1"
            };
            foreach (var setting in values) builder.UseSetting(setting.Key, setting.Value);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
            builder.ConfigureServices(services => services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = 443));
        });

        async Task<HttpContext> SendAsync(IPAddress remoteIp, bool forwardedHttps, string path = "/health/live") =>
            await factory.Server.SendAsync(context =>
            {
                context.Request.Method = "GET";
                context.Request.Scheme = "http";
                context.Request.Host = new HostString("example.test");
                context.Request.Path = path;
                context.Connection.RemoteIpAddress = remoteIp;
                if (forwardedHttps) context.Request.Headers["X-Forwarded-Proto"] = "https";
            });

        var trusted = await SendAsync(IPAddress.Loopback, forwardedHttps: true);
        Assert.AreEqual(1, factory.Services.GetRequiredService<AdminTokenSecurityOptions>().TrustedProxyIps.Count);
        Assert.AreEqual("https", trusted.Request.Scheme, $"Remote IP: {trusted.Connection.RemoteIpAddress}; header: {trusted.Request.Headers["X-Forwarded-Proto"]}");
        Assert.AreEqual(StatusCodes.Status200OK, trusted.Response.StatusCode);
        AssertHeaders(trusted);

        var missing = await SendAsync(IPAddress.Loopback, forwardedHttps: true, path: "/missing.json");
        Assert.AreEqual(StatusCodes.Status404NotFound, missing.Response.StatusCode);
        AssertHeaders(missing);

        var untrusted = await SendAsync(IPAddress.Parse("192.0.2.10"), forwardedHttps: true);
        Assert.AreEqual(StatusCodes.Status308PermanentRedirect, untrusted.Response.StatusCode);
        Assert.AreEqual("https://example.test/health/live", untrusted.Response.Headers.Location.ToString());
        AssertHeaders(untrusted);

        var http = await SendAsync(IPAddress.Loopback, forwardedHttps: false);
        Assert.AreEqual(StatusCodes.Status308PermanentRedirect, http.Response.StatusCode);
        AssertHeaders(http);

        static void AssertHeaders(HttpContext context)
        {
            var headers = context.Response.Headers;
            Assert.AreEqual("nosniff", headers["X-Content-Type-Options"].ToString());
            Assert.AreEqual("DENY", headers["X-Frame-Options"].ToString());
            Assert.AreEqual("no-referrer", headers["Referrer-Policy"].ToString());
            Assert.AreEqual("camera=(), microphone=(), geolocation=()", headers["Permissions-Policy"].ToString());
            Assert.IsTrue(headers["Content-Security-Policy"].ToString().Contains("frame-ancestors 'none'", StringComparison.Ordinal));
        }
    }
}
