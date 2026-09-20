using EasyLotteryApi.Security;
using Microsoft.AspNetCore.SignalR;

namespace EasyLotteryApi.Interactions;

public sealed class InteractionHub : Hub
{
    public const string StateChangedEvent = "InteractionStateChanged";
    private readonly ObsSessionTokenService _tokens;
    private readonly InteractionStateBroadcaster _broadcaster;

    public InteractionHub(ObsSessionTokenService tokens, InteractionStateBroadcaster broadcaster)
    {
        _tokens = tokens;
        _broadcaster = broadcaster;
    }

    public async Task JoinRound(Guid roundId, string sessionToken)
    {
        if (!_tokens.CanAccessObs(sessionToken, ObsResourceKind.InteractionRound, roundId.ToString(), ObsSessionScope.Read))
            throw new HubException("互動回合 OBS 憑證無效。");
        await Groups.AddToGroupAsync(Context.ConnectionId, InteractionRoundGroups.For(roundId));
        await Clients.Caller.SendAsync(StateChangedEvent, await _broadcaster.GetSnapshotAsync(roundId, Context.ConnectionAborted));
    }
}

public static class InteractionRoundGroups
{
    public static string For(Guid roundId) => $"interaction-round:{roundId:N}";
}
