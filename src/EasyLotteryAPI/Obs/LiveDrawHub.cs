using Microsoft.AspNetCore.SignalR;
using EasyLotteryDomain.Models;
using EasyLotteryApi.Security;

namespace EasyLotteryApi.Obs;

public sealed class LiveDrawHub : Hub
{
    private readonly ObsSessionTokenService _tokens;

    public LiveDrawHub(ObsSessionTokenService tokens) => _tokens = tokens;

    public Task JoinSession(string kind, Guid publicId, string sessionToken)
    {
        if (!ObsResourceKinds.TryParse(kind, out var resourceKind)
            || !LiveDrawSessionGroups.TryGetSessionKind(resourceKind, out _)
            || !_tokens.CanAccessObs(sessionToken, resourceKind, publicId.ToString(), ObsSessionScope.Read))
        {
            throw new HubException("OBS 工作階段憑證無效。");
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, LiveDrawSessionGroups.For(resourceKind, publicId));
    }
}

public static class LiveDrawSessionGroups
{
    private static readonly IReadOnlyDictionary<ObsResourceKind, LiveDrawSessionKind> SessionKinds = new Dictionary<ObsResourceKind, LiveDrawSessionKind>
    {
        [ObsResourceKind.PokeBox] = LiveDrawSessionKind.PokeBox,
        [ObsResourceKind.Roulette] = LiveDrawSessionKind.Roulette
    };

    private static readonly IReadOnlyDictionary<LiveDrawSessionKind, ObsResourceKind> ResourceKinds =
        SessionKinds.ToDictionary(item => item.Value, item => item.Key);

    public static bool TryGetSessionKind(ObsResourceKind resourceKind, out LiveDrawSessionKind kind) =>
        SessionKinds.TryGetValue(resourceKind, out kind);

    public static string For(ObsResourceKind resourceKind, Guid publicId) =>
        $"live-draw:{resourceKind.ToValue()}:{publicId:N}";

    public static string For(LiveDrawSessionKind kind, Guid publicId) =>
        For(ResourceKinds[kind], publicId);
}
