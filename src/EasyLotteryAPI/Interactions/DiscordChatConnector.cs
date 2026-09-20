using EasyLotteryDomain.Models.Interactions;

namespace EasyLotteryApi.Interactions;

public sealed record DiscordConnectorHealth(string State, string GuildScope, string ChannelScope);

public sealed class DiscordChatConnector : IPlatformConnector
{
    public string Platform => "Discord";
    public DiscordConnectorHealth Health { get; private set; } = new("unconfigured", "", "");

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        Health = new("degraded", Health.GuildScope, Health.ChannelScope);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Health = new("disabled", Health.GuildScope, Health.ChannelScope);
        return Task.CompletedTask;
    }

    public static InteractionEvent NormalizeMessage(string guildId, string channelId, string userId, string eventId, string content) =>
        new(PlatformKind.Discord, $"{guildId}:{channelId}", eventId, userId, content);
}
