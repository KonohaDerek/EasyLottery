namespace EasyLotteryApi.Security;

public enum ObsResourceKind
{
    Donate,
    PokeBox,
    Roulette,
    Overtime
}

public enum ObsSessionScope
{
    Read,
    Control
}

public static class ObsResourceKinds
{
    private static readonly IReadOnlyDictionary<ObsResourceKind, string> Values = new Dictionary<ObsResourceKind, string>
    {
        [ObsResourceKind.Donate] = "donate",
        [ObsResourceKind.PokeBox] = "pokebox",
        [ObsResourceKind.Roulette] = "roulette",
        [ObsResourceKind.Overtime] = "overtime"
    };

    private static readonly IReadOnlyDictionary<string, ObsResourceKind> ByValue = Values
        .ToDictionary(item => item.Value, item => item.Key, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlySet<ObsResourceKind> All { get; } = new HashSet<ObsResourceKind>(Values.Keys);
    public static IReadOnlySet<ObsResourceKind> LiveDraw { get; } = new HashSet<ObsResourceKind>
    {
        ObsResourceKind.Donate,
        ObsResourceKind.PokeBox,
        ObsResourceKind.Roulette
    };
    public static IReadOnlySet<ObsResourceKind> SettingsWritable { get; } = new HashSet<ObsResourceKind>
    {
        ObsResourceKind.Overtime,
        ObsResourceKind.PokeBox,
        ObsResourceKind.Roulette
    };

    public static bool TryParse(string? value, out ObsResourceKind kind) =>
        ByValue.TryGetValue(value?.Trim() ?? "", out kind);

    public static string ToValue(this ObsResourceKind kind) => Values[kind];
}

public static class ObsSessionScopes
{
    private static readonly IReadOnlyDictionary<ObsSessionScope, string> Values = new Dictionary<ObsSessionScope, string>
    {
        [ObsSessionScope.Read] = "read",
        [ObsSessionScope.Control] = "control"
    };

    private static readonly IReadOnlyDictionary<string, ObsSessionScope> ByValue = Values
        .ToDictionary(item => item.Value, item => item.Key, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlySet<ObsSessionScope> All { get; } = new HashSet<ObsSessionScope>(Values.Keys);

    public static bool TryParse(string? value, out ObsSessionScope scope) =>
        ByValue.TryGetValue(value?.Trim() ?? "", out scope);

    public static string ToValue(this ObsSessionScope scope) => Values[scope];
}
