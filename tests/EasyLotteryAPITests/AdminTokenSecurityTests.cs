using System.Net;
using EasyLotteryApi;
using Microsoft.AspNetCore.Http;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class AdminTokenSecurityTests
{
    [TestMethod]
    public void LocalMode_AllowsLoopbackAndConfiguredClient_ButRejectsRemoteClient()
    {
        var policy = CreatePolicy(AdminTokenSecurityOptions.LocalMode, "203.0.113.10");

        Assert.IsTrue(policy.CanIssue(Context(IPAddress.Loopback)));
        Assert.IsTrue(policy.CanIssue(Context(IPAddress.Parse("203.0.113.10"))));
        Assert.IsFalse(policy.CanIssue(Context(IPAddress.Parse("198.51.100.20"))));
    }

    [TestMethod]
    public void PublicMode_RequiresAllowlistedClient()
    {
        var policy = CreatePolicy(AdminTokenSecurityOptions.PublicMode, "203.0.113.0/24");

        Assert.IsTrue(policy.CanIssue(Context(IPAddress.Parse("203.0.113.42"))));
        Assert.IsFalse(policy.CanIssue(Context(IPAddress.Parse("198.51.100.20"))));
        Assert.IsFalse(policy.CanIssue(Context(IPAddress.Loopback)));
    }

    [TestMethod]
    public void UnknownMode_FailsClosed()
    {
        var policy = CreatePolicy("invalid", "loopback");

        Assert.IsFalse(policy.CanIssue(Context(IPAddress.Loopback)));
    }

    [TestMethod]
    public void TokenService_UsesConfiguredLifetimes()
    {
        var now = DateTimeOffset.UtcNow;
        var tokens = new ObsSessionTokenService(new AdminTokenSecurityOptions
        {
            Mode = AdminTokenSecurityOptions.PublicMode,
            AllowedClientIps = [],
            TrustedProxyIps = [],
            AdminTokenLifetime = TimeSpan.FromMinutes(15),
            ObsTokenLifetime = TimeSpan.FromMinutes(30)
        });

        var admin = tokens.IssueAdminToken();
        var obs = tokens.IssueObsToken("donate", Guid.NewGuid().ToString(), ["read"]);

        Assert.IsTrue(admin.ExpiresAtUtc >= now.AddMinutes(14));
        Assert.IsTrue(admin.ExpiresAtUtc <= now.AddMinutes(16));
        Assert.IsTrue(obs.ExpiresAtUtc >= now.AddMinutes(29));
        Assert.IsTrue(obs.ExpiresAtUtc <= now.AddMinutes(31));
    }

    private static AdminTokenIssuancePolicy CreatePolicy(string mode, params string[] allowedClientIps) =>
        new(new AdminTokenSecurityOptions
        {
            Mode = mode,
            AllowedClientIps = allowedClientIps,
            TrustedProxyIps = [],
            AdminTokenLifetime = TimeSpan.FromMinutes(60),
            ObsTokenLifetime = TimeSpan.FromMinutes(360)
        });

    private static HttpContext Context(IPAddress? remoteIp)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = remoteIp;
        return context;
    }
}
