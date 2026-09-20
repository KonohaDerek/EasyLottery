using EasyLotteryApplication.Interactions;
using EasyLotteryApplication.Settings;
using EasyLotteryDomain.Models.Interactions;

namespace EasyLotteryApiTests.Interactions;

[TestClass]
public sealed class IdentityLinkServiceTests
{
    [TestMethod]
    public async Task LinkCode_IsSingleUseAndExpiredCodesAreRejected()
    {
        var sourceProfile = new AudienceProfile();
        var targetProfile = new AudienceProfile();
        var source = new PlatformIdentity(PlatformKind.YouTube, "yt-user", "channel", sourceProfile.Id, "viewer");
        var target = new PlatformIdentity(PlatformKind.Twitch, "tw-user", "channel", targetProfile.Id, "viewer");
        var document = new InteractionsYamlDocument { AudienceProfiles = [sourceProfile, targetProfile], PlatformIdentities = [source, target] };
        var repository = new MemoryRepository(document);
        var service = new IdentityLinkService(repository, TimeProvider.System);
        var issued = await service.IssueAsync(source.Id, target.Id);

        await service.CompleteAsync(issued.Code);
        var replayRejected = false;
        try { await service.CompleteAsync(issued.Code); } catch (InvalidOperationException) { replayRejected = true; }
        Assert.IsTrue(replayRejected);
        Assert.AreEqual(targetProfile.Id, repository.Document.PlatformIdentities.Single(item => item.Id == source.Id).AudienceProfileId);
    }

    private sealed class MemoryRepository(InteractionsYamlDocument document) : IInteractionsYamlDocumentRepository
    {
        public InteractionsYamlDocument Document { get; } = document;
        public Task<InteractionsYamlDocument> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Document);
        public Task SaveAsync(InteractionsYamlDocument document, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
