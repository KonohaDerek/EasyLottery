using EasyLotteryApi;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class AdminAccessTests
{
    [TestMethod]
    public void IsAuthorized_RequiresMatchingConfiguredToken()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Settings:AdminToken"] = "test-token" }).Build();
        var access = new AdminAccess(configuration);
        var request = new DefaultHttpContext().Request;

        Assert.IsFalse(access.IsAuthorized(request));
        request.Headers[AdminAccess.HeaderName] = "wrong";
        Assert.IsFalse(access.IsAuthorized(request));
        request.Headers[AdminAccess.HeaderName] = "test-token";
        Assert.IsTrue(access.IsAuthorized(request));
    }
}
