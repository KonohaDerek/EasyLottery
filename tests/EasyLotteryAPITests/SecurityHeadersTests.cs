using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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
}
