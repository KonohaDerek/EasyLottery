namespace EasyLotteryWasm.Layout;

/// <summary>
/// The single source of truth for the management navigation.  Keep routes here so
/// the sidebar and its active-group behavior cannot drift apart.
/// </summary>
public static class NavigationCatalog
{
    public const string HomeGroupId = "home";

    public static IReadOnlyList<NavigationGroup> Groups { get; } =
    [
        new(
            "activities",
            "活動管理",
            "▦",
            [
                new("Donate 抽獎活動", "donate-activities", "◎", "建立與管理 Donate 抽獎活動"),
                new("戳戳樂", "pokebox", "✦", "管理戳戳樂活動"),
                new("轉盤", "roulette", "◉", "管理轉盤活動"),
                new("加班台", "activity/overtime", "◷", "管理加班台與直播訊息"),
                new("活動市集", "market", "◇", "瀏覽可用的活動模板")
            ]),
        new(
            "obs",
            "OBS 與直播呈現",
            "▣",
            [
                new("OBS 版型", "system/obs-layouts", "▤", "設定 OBS 畫面版型"),
                new("版面視覺", "system/visual-styles", "◈", "設定色彩與版面風格"),
                new("音效模板", "system/sound-cues", "♫", "設定動畫音效模板"),
                new("OBS 資產庫", "system/obs-assets", "▧", "上傳與管理 OBS 使用的圖片、音效與模型")
            ]),
        new(
            "results",
            "結果與紀錄",
            "≋",
            [
                new("活動與 Donate 結果", "activity-results", "≡", "檢視抽獎結果與紀錄")
            ]),
        new(
            "integrations",
            "串接與支付",
            "⌁",
            [
                new("支付與 Donate 串接", "system/payment", "＄", "設定支付與 Donate 串接"),
                new("YouTube 串接", "system/youtube-login", "▶", "設定 YouTube API")
            ]),
        new(
            "system",
            "系統設定",
            "⚙",
            [
                new("Passkey 管理", "system/access", "♙", "新增與移除管理員 Passkey 裝置"),
                new("審計紀錄", "system/audit", "☷", "檢視系統安全稽核紀錄"),
                new("隱私權政策", "privacy-policy", "▤", "檢視 EasyLottery 隱私權政策"),
                new("關於 EasyLottery", "about", "ⓘ", "檢視 EasyLottery 產品資訊")
            ])
    ];

    public static NavigationGroup Home { get; } = new(
        HomeGroupId,
        "首頁",
        "⌂",
        [new("抽獎首頁／控制台", "", "⌂", "返回抽獎控制台")]);

    public static IEnumerable<NavigationGroup> AllGroups => [Home, .. Groups];

    public static string? FindGroupId(string? relativePath)
    {
        var path = Normalize(relativePath);
        if (path.Length == 0)
        {
            return HomeGroupId;
        }

        return Groups.FirstOrDefault(group => group.Items.Any(item => IsMatch(path, item.Route)))?.Id;
    }

    public static bool IsMatch(string? relativePath, string route)
    {
        var path = Normalize(relativePath);
        var normalizedRoute = Normalize(route);
        return normalizedRoute.Length == 0
            ? path.Length == 0
            : path.Equals(normalizedRoute, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(normalizedRoute + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string? path)
    {
        var normalized = (path ?? string.Empty).Trim();
        var queryStart = normalized.IndexOfAny(['?', '#']);
        if (queryStart >= 0)
        {
            normalized = normalized[..queryStart];
        }

        return normalized.Trim('/');
    }
}

public sealed record NavigationGroup(
    string Id,
    string Label,
    string Icon,
    IReadOnlyList<NavigationItem> Items);

public sealed record NavigationItem(
    string Label,
    string Route,
    string Icon,
    string Description);
