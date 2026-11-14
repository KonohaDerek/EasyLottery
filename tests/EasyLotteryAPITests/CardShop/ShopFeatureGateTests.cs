using System.Net;
using System.Net.Http.Json;
using EasyLotteryApi;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EasyLotteryApiTests.CardShop;

[TestClass]
public sealed class ShopFeatureGateTests
{
    [TestMethod]
    public async Task FeatureStatus_DefaultsToDisabled_AndShopApisFailClosed()
    {
        await using var factory = CreateFactory(enabled: null);
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<FeatureStatus>("/api/shop/feature-status");
        using var api = await client.GetAsync("/api/shop/catalog");

        Assert.IsFalse(status!.Enabled);
        Assert.AreEqual(HttpStatusCode.NotFound, api.StatusCode);
        StringAssert.Contains(await api.Content.ReadAsStringAsync(), "shop_disabled");
    }

    [TestMethod]
    public async Task FeatureStatus_ReflectsExplicitEnableSetting_AndDoesNotGateNonShopApis()
    {
        await using var factory = CreateFactory(enabled: true);
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<FeatureStatus>("/api/shop/feature-status");
        using var api = await client.GetAsync("/api/not-a-shop-endpoint");

        Assert.IsTrue(status!.Enabled);
        Assert.AreEqual(HttpStatusCode.NotFound, api.StatusCode);
        Assert.IsFalse((await api.Content.ReadAsStringAsync()).Contains("shop_disabled", StringComparison.Ordinal));
    }

    private static WebApplicationFactory<Program> CreateFactory(bool? enabled) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["Storage:Directory"] = Path.Combine(Path.GetTempPath(), $"easy-lottery-shop-gate-tests-{Guid.NewGuid():N}")
            };
            if (enabled.HasValue) values["Shop:Enabled"] = enabled.Value.ToString();
            configuration.AddInMemoryCollection(values);
        }));

    private sealed record FeatureStatus(bool Enabled);
}
