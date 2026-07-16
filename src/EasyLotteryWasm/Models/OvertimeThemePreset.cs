using System;
using System.Collections.Generic;
using System.Linq;

namespace EasyLotteryWasm.Models
{
    public sealed record OvertimeThemePreset(
        string Key,
        string Name,
        string Description,
        string Emoji,
        string BackgroundStart,
        string BackgroundEnd,
        string SurfaceColor,
        string SurfaceBorderColor,
        string AccentColor,
        string AccentSoftColor,
        string GlowColor)
    {
        public string BuildCssVariables()
        {
            return string.Join("; ", new[]
            {
                $"--overtime-background-start: {BackgroundStart}",
                $"--overtime-background-end: {BackgroundEnd}",
                $"--overtime-surface: {SurfaceColor}",
                $"--overtime-surface-border: {SurfaceBorderColor}",
                $"--overtime-accent: {AccentColor}",
                $"--overtime-accent-soft: {AccentSoftColor}",
                $"--overtime-glow: {GlowColor}"
            });
        }

        public string BuildPreviewStyle()
        {
            return
                $"background: " +
                $"radial-gradient(circle at 18% 18%, color-mix(in srgb, {AccentSoftColor} 32%, transparent), transparent 34%), " +
                $"radial-gradient(circle at 82% 0%, color-mix(in srgb, {GlowColor} 28%, transparent), transparent 30%), " +
                $"linear-gradient(135deg, {BackgroundStart}, {BackgroundEnd}); " +
                $"border-color: {SurfaceBorderColor};";
        }

        public static IReadOnlyList<OvertimeThemePreset> Catalog { get; } = new[]
        {
            new OvertimeThemePreset(
                "festival-stage",
                "祭典舞台",
                "明亮、熱鬧、適合活動感很強的加班台。",
                "🎪",
                "#140d22",
                "#311947",
                "rgba(25, 17, 39, 0.92)",
                "rgba(255, 207, 121, 0.35)",
                "#ffd166",
                "#ff7ab6",
                "rgba(255, 209, 102, 0.46)"),
            new OvertimeThemePreset(
                "arcade-neon",
                "霓虹街機",
                "高對比、速度感強，像街機螢幕一樣俐落。",
                "🎮",
                "#060816",
                "#0f1b3d",
                "rgba(10, 16, 34, 0.92)",
                "rgba(84, 240, 255, 0.3)",
                "#4de1ff",
                "#7cffcb",
                "rgba(77, 225, 255, 0.42)"),
            new OvertimeThemePreset(
                "gacha-loot",
                "扭蛋寶箱",
                "偏向抽卡 / 開箱感，適合強調贊助與驚喜揭曉。",
                "🎁",
                "#1b102a",
                "#3c1e57",
                "rgba(40, 22, 63, 0.94)",
                "rgba(255, 191, 105, 0.34)",
                "#ffb84d",
                "#ff8fc0",
                "rgba(255, 184, 77, 0.48)")
        };

        public static OvertimeThemePreset GetByKey(string? key)
        {
            return Catalog.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
                ?? Catalog[0];
        }
    }
}
