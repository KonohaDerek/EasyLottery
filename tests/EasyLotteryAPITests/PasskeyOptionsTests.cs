using Microsoft.Extensions.Configuration;

using EasyLotteryApi;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class PasskeyOptionsTests
{
    [TestMethod]
    public void Load_ReadsEmailRpIdOriginsAndClampsChallengeLifetime()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:Email"] = " Owner@Example.com ",
                ["Admin:Passkey:RpId"] = "example.com",
                ["Admin:Passkey:ServerName"] = "EasyLottery Production",
                ["Admin:Passkey:Origins:0"] = "https://example.com",
                ["Admin:Passkey:Origin"] = "https://admin.example.com",
                ["Admin:Passkey:ChallengeMinutes"] = "99"
            })
            .Build();

        var options = AdminPasskeyOptions.Load(configuration);

        Assert.AreEqual("owner@example.com", options.Email);
        Assert.AreEqual("example.com", options.RpId);
        Assert.AreEqual("EasyLottery Production", options.ServerName);
        CollectionAssert.AreEquivalent(
            new[] { "https://example.com", "https://admin.example.com" },
            options.Origins.ToArray());
        Assert.AreEqual(TimeSpan.FromMinutes(10), options.ChallengeLifetime);
    }

    [TestMethod]
    public void Load_UsesSafeLocalDefaults()
    {
        var options = AdminPasskeyOptions.Load(new ConfigurationBuilder().Build());

        Assert.AreEqual(AdminPasskeyOptions.DefaultEmail, options.Email);
        Assert.AreEqual("localhost", options.RpId);
        Assert.IsTrue(options.Origins.Contains("http://localhost:18930"));
        Assert.AreEqual(TimeSpan.FromMinutes(2), options.ChallengeLifetime);
    }

    [TestMethod]
    public void Load_ClampsChallengeLifetimeToOneMinuteMinimum()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Admin:Passkey:ChallengeMinutes"] = "0"
            })
            .Build();

        var options = AdminPasskeyOptions.Load(configuration);

        Assert.AreEqual(TimeSpan.FromMinutes(1), options.ChallengeLifetime);
    }
}
