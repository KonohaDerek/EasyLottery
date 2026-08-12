using EasyLotteryApi.Security;
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
        var request = RequestWithToken(tokens.IssueObsToken(ObsResourceKind.Donate, Guid.NewGuid().ToString(), [ObsSessionScope.Read]).Token);

        Assert.AreEqual(ApiAccessDecision.Forbidden, access.RequireAdmin(request));
    }

    [TestMethod]
    public void ObsToken_IsRestrictedToResourceAndScope()
    {
        var tokens = new ObsSessionTokenService();
        var access = new ObsSessionAccess(tokens);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var request = RequestWithToken(tokens.IssueObsToken(ObsResourceKind.Donate, first.ToString(), [ObsSessionScope.Read]).Token);

        Assert.AreEqual(ApiAccessDecision.Allowed, access.RequireObs(request, ObsResourceKind.Donate, first.ToString(), ObsSessionScope.Read));
        Assert.AreEqual(ApiAccessDecision.Forbidden, access.RequireObs(request, ObsResourceKind.Donate, first.ToString(), ObsSessionScope.Control));
        Assert.AreEqual(ApiAccessDecision.Forbidden, access.RequireObs(request, ObsResourceKind.Donate, second.ToString(), ObsSessionScope.Read));
        Assert.AreEqual(ApiAccessDecision.Forbidden, access.RequireObs(request, ObsResourceKind.Roulette, first.ToString(), ObsSessionScope.Read));
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
        var issued = tokens.IssueObsToken(ObsResourceKind.Overtime, "default", [ObsSessionScope.Read], TimeSpan.FromMilliseconds(20));
        await Task.Delay(80);

        Assert.IsFalse(tokens.CanAccessObs(issued.Token, ObsResourceKind.Overtime, "default", ObsSessionScope.Read));
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
        Assert.AreEqual(ApiAccessDecision.Forbidden, access.RequireObs(context.Request, ObsResourceKind.Overtime, "default", ObsSessionScope.Read));
    }

    [TestMethod]
    public void ScopedObsToken_CanUseLegacyQueryString()
    {
        var tokens = new ObsSessionTokenService();
        var access = new ObsSessionAccess(tokens);
        var context = new DefaultHttpContext();
        var resourceId = Guid.NewGuid();
        var issued = tokens.IssueObsToken(ObsResourceKind.Donate, resourceId.ToString(), [ObsSessionScope.Read]).Token;
        context.Request.QueryString = new QueryString($"?{ObsSessionAccess.QueryName}={Uri.EscapeDataString(issued)}");

        Assert.AreEqual(ApiAccessDecision.Allowed, access.RequireObs(context.Request, ObsResourceKind.Donate, resourceId.ToString(), ObsSessionScope.Read));
    }

    private static HttpRequest RequestWithToken(string token)
    {
        var request = new DefaultHttpContext().Request;
        request.Headers[ObsSessionTokenService.HeaderName] = token;
        return request;
    }
}
