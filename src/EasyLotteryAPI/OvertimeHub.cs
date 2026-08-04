using Microsoft.AspNetCore.SignalR;

public sealed class OvertimeHub : Hub
{
    public const string GroupName = "overtime:default";
    private readonly EasyLotteryApi.ObsSessionTokenService _tokens;

    public OvertimeHub(EasyLotteryApi.ObsSessionTokenService tokens) => _tokens = tokens;

    public Task Join(string token)
    {
        if (!_tokens.CanAccessObs(token, "overtime", "default", "read"))
            throw new HubException("加班台 OBS 憑證無效。");
        return Groups.AddToGroupAsync(Context.ConnectionId, GroupName);
    }
}
