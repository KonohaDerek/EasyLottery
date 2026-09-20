namespace EasyLotteryApi.Interactions;

public sealed class TwitchChatConnector : IPlatformConnector
{
    public string Platform => "Twitch";
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
