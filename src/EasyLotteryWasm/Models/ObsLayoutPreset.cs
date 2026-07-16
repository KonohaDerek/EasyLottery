namespace EasyLotteryWasm.Models
{
    public sealed class ObsLayoutPreset
    {
        public string Key { get; init; } = "";

        public string Name { get; init; } = "";

        public string Description { get; init; } = "";

        public string Tagline { get; init; } = "";

        public string StageClassName { get; init; } = "";

        public string StateClassName { get; init; } = "";

        public string ControlClassName { get; init; } = "";

        public string AccentGradient { get; init; } = "";

        public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    }

    public static class ObsLayoutCatalog
    {
        public const string DefaultLayoutKey = "stage-spotlight";

        private static readonly IReadOnlyList<ObsLayoutPreset> Presets = new[]
        {
            new ObsLayoutPreset
            {
                Key = "stage-spotlight",
                Name = "舞台聚光燈",
                Description = "以單一卡片為中心，強調資訊焦點與直播感，適合大多數 OBS 場景。",
                Tagline = "Stage / Spotlight / Focus",
                StageClassName = "obs-stage-layout obs-stage-layout-spotlight",
                StateClassName = "obs-state obs-state-spotlight",
                ControlClassName = "obs-controls obs-controls-spotlight",
                AccentGradient = "linear-gradient(135deg, #ffcb47, #ff9f1a)",
                Tags = new[] { "預設", "集中焦點", "通用" }
            },
            new ObsLayoutPreset
            {
                Key = "neon-panel",
                Name = "霓虹分割面板",
                Description = "更強烈的對比與面板分區，適合轉盤與戳戳樂這種節奏明確的畫面。",
                Tagline = "Neon / Split / Arcade",
                StageClassName = "obs-stage-layout obs-stage-layout-neon",
                StateClassName = "obs-state obs-state-neon",
                ControlClassName = "obs-controls obs-controls-neon",
                AccentGradient = "linear-gradient(135deg, #63d4ff, #8b5cf6)",
                Tags = new[] { "高能量", "分區", "街機" }
            },
            new ObsLayoutPreset
            {
                Key = "festival-pane",
                Name = "祭典舞台面板",
                Description = "偏暖色和節慶感的版型，適合加班台與活動總覽畫面。",
                Tagline = "Festival / Warm / Celebration",
                StageClassName = "obs-stage-layout obs-stage-layout-festival",
                StateClassName = "obs-state obs-state-festival",
                ControlClassName = "obs-controls obs-controls-festival",
                AccentGradient = "linear-gradient(135deg, #ffd36c, #ef6b6b)",
                Tags = new[] { "活動", "暖色", "主持" }
            }
        };

        public static IReadOnlyList<ObsLayoutPreset> All => Presets;

        public static ObsLayoutPreset GetByKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return Presets[0];
            }

            return Presets.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                ?? Presets[0];
        }

        public static string NormalizeKey(string? key) => GetByKey(key).Key;
    }
}