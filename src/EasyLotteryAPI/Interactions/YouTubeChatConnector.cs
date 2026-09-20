namespace EasyLotteryApi.Interactions;

public sealed class YouTubeChatConnector : IPlatformConnector
{
    public string Platform => "YouTube";
    public TimeSpan PollingInterval { get; private set; } = TimeSpan.FromSeconds(5);

    public void RespectPollingInterval(TimeSpan interval) => PollingInterval = interval <= TimeSpan.Zero ? TimeSpan.FromSeconds(5) : interval;
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
