using EasyLotteryDomain.Models.Interactions;
using EasyLotteryDomain.Models.Config;

namespace EasyLotteryDomainTests.Interactions;

[TestClass]
public sealed class InteractionModelsTests
{
    [TestMethod]
    public void InteractionDocument_StartsWithEmptyCollections()
    {
        var document = new InteractionsYamlDocument();

        Assert.AreEqual(YamlDocumentSchema.CurrentVersion, document.ConfigVersion);
        Assert.HasCount(0, document.AudienceProfiles);
        Assert.HasCount(0, document.PlatformIdentities);
        Assert.HasCount(0, document.Rounds);
        Assert.HasCount(0, document.ProcessedEventKeys);
    }

    [TestMethod]
    public void PlatformIdentity_UsesStableExternalIdentityWithinPlatformScope()
    {
        var profileId = Guid.NewGuid();
        var identity = new PlatformIdentity(PlatformKind.YouTube, "UC123", "channel-abc", profileId, "Derek");

        Assert.AreEqual("YouTube:channel-abc:UC123", identity.EventScopeKey);
        Assert.AreEqual(profileId, identity.AudienceProfileId);
    }

    [TestMethod]
    public void InteractionDocument_PreservesProcessedEventKeysForReplayProtection()
    {
        var document = new InteractionsYamlDocument
        {
            ProcessedEventKeys = ["YouTube:channel-abc:event-1"]
        };

        Assert.IsTrue(document.ProcessedEventKeys.Contains("YouTube:channel-abc:event-1"));
    }
}
