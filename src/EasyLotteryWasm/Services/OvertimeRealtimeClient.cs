using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace EasyLotteryWasm.Services;

public sealed class OvertimeRealtimeClient : IAsyncDisposable
{
    private readonly NavigationManager _navigationManager;
    private readonly ObsSessionService _sessionService;
    private HubConnection? _connection;

    public event Func<Task>? StateChanged;
    public event Func<Task>? FeedChanged;

    public OvertimeRealtimeClient(NavigationManager navigationManager, ObsSessionService sessionService)
    {
        _navigationManager = navigationManager;
        _sessionService = sessionService;
    }

    public async Task StartAsync()
    {
        if (_connection is not null)
        {
            return;
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(_navigationManager.ToAbsoluteUri("/hubs/overtime"), options =>
            {
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On("OvertimeStateChanged", async () => await NotifyAsync(StateChanged));
        _connection.On("OvertimeFeedChanged", async () => await NotifyAsync(FeedChanged));
        await _connection.StartAsync();
        await JoinAsync();
        _connection.Reconnected += async _ => await JoinAsync();
    }

    private async Task JoinAsync() =>
        await _connection!.InvokeAsync("Join", await _sessionService.GetSessionTokenAsync());

    private static async Task NotifyAsync(Func<Task>? handlers)
    {
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<Func<Task>>())
        {
            await handler();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is null)
        {
            return;
        }

        await _connection.DisposeAsync();
    }
}
