using EasyLotteryApi.Security;

namespace EasyLotteryApiTests.Interactions;

[TestClass]
public sealed class InteractionHubTests
{
    [TestMethod]
    public void InteractionRoundObsToken_IsScopedToItsRoundAndReadOnly()
    {
        var tokens = new ObsSessionTokenService();
        var roundId = Guid.NewGuid();
        var token = tokens.IssueObsToken(ObsResourceKind.InteractionRound, roundId.ToString(), [ObsSessionScope.Read]).Token;

        Assert.IsTrue(tokens.CanAccessObs(token, ObsResourceKind.InteractionRound, roundId.ToString(), ObsSessionScope.Read));
        Assert.IsFalse(tokens.CanAccessObs(token, ObsResourceKind.InteractionRound, roundId.ToString(), ObsSessionScope.Control));
        Assert.IsFalse(tokens.CanAccessObs(token, ObsResourceKind.InteractionRound, Guid.NewGuid().ToString(), ObsSessionScope.Read));
    }
}
