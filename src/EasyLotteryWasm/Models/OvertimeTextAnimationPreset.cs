using System.Collections.Generic;
using System.Linq;

namespace EasyLotteryWasm.Models
{
    public sealed class OvertimeTextAnimationPreset
    {
        public string Key { get; init; } = "";

        public string Name { get; init; } = "";

        public string Description { get; init; } = "";

        public static IReadOnlyList<OvertimeTextAnimationPreset> Catalog { get; } = new[]
        {
            new OvertimeTextAnimationPreset
            {
                Key = "slide-in",
                Name = "滑入",
                Description = "文字從左側滑入並落位。"
            },
            new OvertimeTextAnimationPreset
            {
                Key = "fade-in",
                Name = "淡入",
                Description = "文字淡入顯示，節奏較柔和。"
            },
            new OvertimeTextAnimationPreset
            {
                Key = "pop-in",
                Name = "彈出",
                Description = "文字快速彈出，適合強調新 donate。"
            },
            new OvertimeTextAnimationPreset
            {
                Key = "none",
                Name = "無動畫",
                Description = "直接顯示，不套用進場動作。"
            }
        };

        public static OvertimeTextAnimationPreset GetByKey(string? key)
        {
            var normalizedKey = NormalizeKey(key);
            return Catalog.FirstOrDefault(item => string.Equals(item.Key, normalizedKey, System.StringComparison.OrdinalIgnoreCase)) ?? Catalog[0];
        }

        public static string NormalizeKey(string? key)
        {
            return Catalog.Any(item => string.Equals(item.Key, key, System.StringComparison.OrdinalIgnoreCase))
                ? key!.Trim()
                : Catalog[0].Key;
        }
    }
}