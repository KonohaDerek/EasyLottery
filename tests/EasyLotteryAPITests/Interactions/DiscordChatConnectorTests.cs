using EasyLotteryApi.Interactions;
using EasyLotteryDomain.Models.Interactions;

namespace EasyLotteryApiTests.Interactions;

[TestClass]
public sealed class DiscordChatConnectorTests
{
    [TestMethod]
    public void NormalizeMessage_UsesGuildChannelScopeAndDiscordPlatform()
    {
        var message = DiscordChatConnector.NormalizeMessage("guild-1", "channel-2", "user-3", "event-4", "!join");
        Assert.AreEqual(PlatformKind.Discord, message.Platform);
        Assert.AreEqual("guild-1:channel-2", message.ChannelScope);
        Assert.AreEqual("event-4", message.ExternalEventId);
        Assert.AreEqual("!join", message.Content);
    }
}
