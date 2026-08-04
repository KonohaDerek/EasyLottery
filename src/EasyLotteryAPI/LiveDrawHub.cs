using Microsoft.AspNetCore.SignalR;

namespace EasyLotteryApi;

public sealed class LiveDrawHub : Hub
{
    private readonly ObsSessionTokenService _tokens;

    public LiveDrawHub(ObsSessionTokenService tokens) => _tokens = tokens;

    public Task JoinSession(string kind, Guid publicId, string sessionToken)
    {
        if (!_tokens.IsValid(sessionToken))
        {
            throw new HubException("OBS 工作階段憑證無效。");
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, LiveDrawSessionGroups.For(kind, publicId));
    }
}

public static class LiveDrawSessionGroups
{
    public static string For(string kind, Guid publicId) =>
        $"live-draw:{kind.Trim().ToLowerInvariant()}:{publicId:N}";
}
