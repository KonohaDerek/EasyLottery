using System.Collections.Generic;
using System.Linq;

namespace EasyLotteryWasm.Models
{
    public sealed class OvertimeMessageTemplatePreset
    {
        public string Key { get; init; } = "";

        public string Name { get; init; } = "";

        public string Description { get; init; } = "";

        public static IReadOnlyList<OvertimeMessageTemplatePreset> Catalog { get; } = new[]
        {
            new OvertimeMessageTemplatePreset
            {
                Key = "default",
                Name = "標準單行",
                Description = "維持現在的簡潔單行資訊列。"
            },
            new OvertimeMessageTemplatePreset
            {
                Key = "pixel-hud",
                Name = "點陣 Q 版",
                Description = "使用像遊戲 HUD 的卡片布局，頭像與訊息更接近點陣圖 Q 版風格。"
            }
        };

        public static OvertimeMessageTemplatePreset GetByKey(string? key)
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