namespace EasyLotteryWasm.Models
{
    public sealed class VisualStylePreset
    {
        public string Key { get; init; } = "";

        public string Name { get; init; } = "";

        public string Description { get; init; } = "";

        public string Tagline { get; init; } = "";

        public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    }

    public static class VisualStyleCatalog
    {
        public const string DefaultThemeKey = "dashboard-console";

        private static readonly IReadOnlyList<VisualStylePreset> Presets = new[]
        {
            new VisualStylePreset
            {
                Key = "dashboard-console",
                Name = "控制台藍白",
                Description = "清爽的藍白作業台風格，適合抽獎首頁、管理頁與一般操作畫面。",
                Tagline = "Dashboard / Clean / Studio",
                Tags = new[] { "預設", "清爽", "管理介面" }
            },
            new VisualStylePreset
            {
                Key = "arcade-neon",
                Name = "霓虹街機",
                Description = "高對比、快速閃耀，適合轉盤與節奏感很強的直播畫面。",
                Tagline = "Neon / Arcade / Motion",
                Tags = new[] { "推薦", "高能量", "直播感" }
            },
            new VisualStylePreset
            {
                Key = "gacha-loot",
                Name = "扭蛋寶箱",
                Description = "偏獎勵、開箱與稀有掉落感，適合戳戳樂與抽卡型直播。",
                Tagline = "Gacha / Loot / Reveal",
                Tags = new[] { "抽卡", "開箱", "稀有度" }
            },
            new VisualStylePreset
            {
                Key = "festival-stage",
                Name = "祭典舞台",
                Description = "溫暖、熱鬧、像節目舞台的活動風格，適合全場型直播。",
                Tagline = "Festival / Stage / Celebration",
                Tags = new[] { "活動", "主持", "舞台感" }
            }
        };

        public static IReadOnlyList<VisualStylePreset> All => Presets;

        public static VisualStylePreset GetByKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return Presets[0];
            }

            return Presets.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                ?? Presets[0];
        }

        public static string NormalizeKey(string? key)
        {
            if (string.Equals(key, "arcade-neon", StringComparison.OrdinalIgnoreCase))
            {
                return DefaultThemeKey;
            }

            return GetByKey(key).Key;
        }
    }
}
