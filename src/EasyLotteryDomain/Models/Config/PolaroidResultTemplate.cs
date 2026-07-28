namespace EasyLotteryDomain.Models.Config;

public sealed record PolaroidResultTemplate(string Key, string DisplayName, string FallbackCongratulation);

public static class PolaroidResultTemplateCatalog
{
    public const string ClassicKey = "classic";

    public static IReadOnlyList<PolaroidResultTemplate> BuiltIns { get; } =
    [
        new(ClassicKey, "經典拍立得", "恭喜 {donor} 抽中 {prize}！"),
        new("celebration", "慶典拍立得", "{donor} 的幸運時刻！{prize} 已經登場！")
    ];

    public static PolaroidResultTemplate Resolve(string? key) => BuiltIns.FirstOrDefault(template =>
        string.Equals(template.Key, key, StringComparison.OrdinalIgnoreCase)) ?? BuiltIns[0];
}
