using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace EasyLotteryWasm.Services;

public sealed record InteractionRoundSnapshot(Guid Id, string Name, string Status, string Type, IReadOnlyList<string> VoteOptions, IReadOnlyDictionary<string, int> VoteCounts, IReadOnlyList<InteractionLeaderboardEntry> Leaderboard);
public sealed record InteractionLeaderboardEntry(Guid AudienceProfileId, string DisplayName, int Points);

public sealed class InteractionRealtimeClient(NavigationManager navigation, ObsSessionService sessionService) : IAsyncDisposable
{
    private HubConnection? _connection;
    private Guid _roundId;
    public event Func<InteractionRoundSnapshot?, Task>? StateChanged;

    public async Task StartAsync(Guid roundId, CancellationToken cancellationToken = default)
    {
        _roundId = roundId;
        if (_connection is null)
        {
            _connection = new HubConnectionBuilder().WithUrl(navigation.ToAbsoluteUri("/hubs/interactions")).WithAutomaticReconnect().Build();
            _connection.On<InteractionRoundSnapshot?>("InteractionStateChanged", NotifyAsync);
            _connection.Reconnected += async _ => await JoinAsync(CancellationToken.None);
            await _connection.StartAsync(cancellationToken);
        }
        await JoinAsync(cancellationToken);
    }

    private async Task JoinAsync(CancellationToken cancellationToken)
    {
        if (_connection is null || _roundId == Guid.Empty) return;
        await _connection.InvokeAsync("JoinRound", _roundId, await sessionService.GetSessionTokenAsync(cancellationToken), cancellationToken);
    }

    private async Task NotifyAsync(InteractionRoundSnapshot? snapshot)
    {
        var handlers = StateChanged;
        if (handlers is null) return;
        foreach (var handler in handlers.GetInvocationList().Cast<Func<InteractionRoundSnapshot?, Task>>()) await handler(snapshot);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
    }
}
