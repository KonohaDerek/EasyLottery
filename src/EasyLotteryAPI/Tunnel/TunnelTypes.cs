namespace EasyLotteryApi.Tunnel;

public enum TunnelProvider
{
    CloudflareQuick,
    DevTunnels
}

public enum TunnelRuntimeState
{
    Stopped,
    Starting,
    Running,
    Failed
}

public static class TunnelProviders
{
    private static readonly IReadOnlyDictionary<TunnelProvider, string> Values = new Dictionary<TunnelProvider, string>
    {
        [TunnelProvider.CloudflareQuick] = "cloudflare-quick",
        [TunnelProvider.DevTunnels] = "dev-tunnels"
    };

    private static readonly IReadOnlyDictionary<string, TunnelProvider> ByValue = Values
        .ToDictionary(item => item.Value, item => item.Key, StringComparer.OrdinalIgnoreCase);

    public static bool TryParse(string? value, out TunnelProvider provider) =>
        ByValue.TryGetValue(value?.Trim() ?? "", out provider);

    public static string ToValue(this TunnelProvider provider) => Values[provider];
}

public static class TunnelRuntimeStates
{
    private static readonly IReadOnlyDictionary<TunnelRuntimeState, string> Values = new Dictionary<TunnelRuntimeState, string>
    {
        [TunnelRuntimeState.Stopped] = "stopped",
        [TunnelRuntimeState.Starting] = "starting",
        [TunnelRuntimeState.Running] = "running",
        [TunnelRuntimeState.Failed] = "failed"
    };

    public static string ToValue(this TunnelRuntimeState state) => Values[state];
}
