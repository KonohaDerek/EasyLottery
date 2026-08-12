namespace EasyLotteryApi.Passkey;

public enum PasskeyFlow
{
    Register,
    Login,
    Add
}

public static class PasskeyFlows
{
    private static readonly IReadOnlyDictionary<PasskeyFlow, string> Values = new Dictionary<PasskeyFlow, string>
    {
        [PasskeyFlow.Register] = "register",
        [PasskeyFlow.Login] = "login",
        [PasskeyFlow.Add] = "add"
    };

    private static readonly IReadOnlyDictionary<string, PasskeyFlow> ByValue = Values
        .ToDictionary(item => item.Value, item => item.Key, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlySet<PasskeyFlow> Authentication { get; } = new HashSet<PasskeyFlow>
    {
        PasskeyFlow.Register,
        PasskeyFlow.Login
    };

    public static bool TryParse(string? value, out PasskeyFlow flow) =>
        ByValue.TryGetValue(value?.Trim() ?? "", out flow);

    public static string ToValue(this PasskeyFlow flow) => Values[flow];
}
