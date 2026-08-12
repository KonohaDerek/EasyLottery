using Microsoft.AspNetCore.SignalR;
using EasyLotteryApi.Security;

namespace EasyLotteryApi.Obs;

public sealed class OvertimeHub : Hub
{
    public const string GroupName = "overtime:default";
    private readonly ObsSessionTokenService _tokens;

    public OvertimeHub(ObsSessionTokenService tokens) => _tokens = tokens;

    public Task Join(string token)
    {
        if (!_tokens.CanAccessObs(token, ObsResourceKind.Overtime, "default", ObsSessionScope.Read))
            throw new HubException("加班台 OBS 憑證無效。");
        return Groups.AddToGroupAsync(Context.ConnectionId, GroupName);
    }
}
