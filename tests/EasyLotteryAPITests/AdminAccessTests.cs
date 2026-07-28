using EasyLotteryApi;
using Microsoft.AspNetCore.Http;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class ObsSessionAccessTests
{
    [TestMethod]
    public void IssueToken_CreatesIndependentTokens()
    {
        var tokens = new ObsSessionTokenService();

        var first = tokens.IssueToken();
        var second = tokens.IssueToken();

        Assert.AreNotEqual(first.Token, second.Token);
        Assert.IsTrue(tokens.IsValid(first.Token));
        Assert.IsTrue(tokens.IsValid(second.Token));
    }

    [TestMethod]
    public void IsAuthorized_RequiresMatchingSessionToken()
    {
        var tokens = new ObsSessionTokenService();
        var issued = tokens.IssueToken().Token;
        var access = new ObsSessionAccess(tokens);
        var request = new DefaultHttpContext().Request;

        Assert.IsFalse(access.IsAuthorized(request));
        request.Headers[ObsSessionTokenService.HeaderName] = "wrong";
        Assert.IsFalse(access.IsAuthorized(request));
        request.Headers[ObsSessionTokenService.HeaderName] = issued;
        Assert.IsTrue(access.IsAuthorized(request));
    }

    [TestMethod]
    public void IsAuthorized_AllowsQueryStringToken()
    {
        var tokens = new ObsSessionTokenService();
        var issued = tokens.IssueToken().Token;
        var access = new ObsSessionAccess(tokens);
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString($"?{ObsSessionAccess.QueryName}={Uri.EscapeDataString(issued)}");

        Assert.IsTrue(access.IsAuthorized(context.Request));
    }
}
