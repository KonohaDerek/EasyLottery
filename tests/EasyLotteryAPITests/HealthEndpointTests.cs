using System.Net;
using EasyLotteryApi;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class HealthEndpointTests
{
    [TestMethod]
    public async Task LivenessAndReadiness_ReturnSuccessfulHealthStatus()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var liveResponse = await client.GetAsync("/health/live");
        using var readyResponse = await client.GetAsync("/health/ready");

        Assert.AreEqual(HttpStatusCode.OK, liveResponse.StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, readyResponse.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            var values = new Dictionary<string, string?>
            {
                ["Storage:Directory"] = Path.Combine(Path.GetTempPath(), $"easy-lottery-health-tests-{Guid.NewGuid():N}")
            };

            foreach (var setting in values) builder.UseSetting(setting.Key, setting.Value);
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(values));
        });
}
