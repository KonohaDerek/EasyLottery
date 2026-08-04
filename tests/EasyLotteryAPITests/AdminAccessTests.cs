using EasyLotteryApi;
using Microsoft.AspNetCore.Http;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class AdminAccessTests
{
    [TestMethod]
    public void AdminTokens_AreIndependentAndAdminOnly()
    {
        var tokens = new ObsSessionTokenService();
        var first = tokens.IssueAdminToken();
        var second = tokens.IssueAdminToken();

        Assert.AreNotEqual(first.Token, second.Token);
        Assert.IsTrue(tokens.IsAdmin(first.Token));
        Assert.IsTrue(tokens.IsAdmin(second.Token));
    }

    [TestMethod]
    public void ObsToken_CannotAuthorizeAdminEndpoint()
    {
        var tokens = new ObsSessionTokenService();
        var access = new ObsSessionAccess(tokens);
        var request = RequestWithToken(tokens.IssueObsToken("donate", Guid.NewGuid().ToString(), ["read"]).Token);

        Assert.AreEqual(ApiAccessDecision.Forbidden, access.RequireAdmin(request));
    }

    [TestMethod]
    public void ObsToken_IsRestrictedToResourceAndScope()
    {
        var tokens = new ObsSessionTokenService();
        var access = new ObsSessionAccess(tokens);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var request = RequestWithToken(tokens.IssueObsToken("donate", first.ToString(), ["read"]).Token);

        Assert.AreEqual(ApiAccessDecision.Allowed, access.RequireObs(request, "donate", first.ToString(), "read"));
        Assert.AreEqual(ApiAccessDecision.Forbidden, access.RequireObs(request, "donate", first.ToString(), "control"));
        Assert.AreEqual(ApiAccessDecision.Forbidden, access.RequireObs(request, "donate", second.ToString(), "read"));
        Assert.AreEqual(ApiAccessDecision.Forbidden, access.RequireObs(request, "roulette", first.ToString(), "read"));
    }

    [TestMethod]
    public void RevokedToken_IsRejected()
    {
        var tokens = new ObsSessionTokenService();
        var issued = tokens.IssueAdminToken();
        tokens.Revoke(issued.Token);

        Assert.IsFalse(tokens.IsAdmin(issued.Token));
    }

    [TestMethod]
    public async Task ExpiredToken_IsRejected()
    {
        var tokens = new ObsSessionTokenService();
        var issued = tokens.IssueObsToken("overtime", "default", ["read"], TimeSpan.FromMilliseconds(20));
        await Task.Delay(80);

        Assert.IsFalse(tokens.CanAccessObs(issued.Token, "overtime", "default", "read"));
    }

    [TestMethod]
    public void AdminToken_CannotUseQueryString()
    {
        var tokens = new ObsSessionTokenService();
        var access = new ObsSessionAccess(tokens);
        var context = new DefaultHttpContext();
        var issued = tokens.IssueAdminToken().Token;
        context.Request.QueryString = new QueryString($"?{ObsSessionAccess.QueryName}={Uri.EscapeDataString(issued)}");

        Assert.AreEqual(ApiAccessDecision.Unauthorized, access.RequireAdmin(context.Request));
        Assert.AreEqual(ApiAccessDecision.Forbidden, access.RequireObs(context.Request, "overtime", "default", "read"));
    }

    [TestMethod]
    public void ScopedObsToken_CanUseLegacyQueryString()
    {
        var tokens = new ObsSessionTokenService();
        var access = new ObsSessionAccess(tokens);
        var context = new DefaultHttpContext();
        var resourceId = Guid.NewGuid();
        var issued = tokens.IssueObsToken("donate", resourceId.ToString(), ["read"]).Token;
        context.Request.QueryString = new QueryString($"?{ObsSessionAccess.QueryName}={Uri.EscapeDataString(issued)}");

        Assert.AreEqual(ApiAccessDecision.Allowed, access.RequireObs(context.Request, "donate", resourceId.ToString(), "read"));
    }

    private static HttpRequest RequestWithToken(string token)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers[ObsSessionTokenService.HeaderName] = token;
        return request;
    }
}
