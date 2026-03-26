using System;
using System.Collections.Generic;

namespace EasyLotteryDomain.Models.Config
{
    public sealed class DrawingRulePreset
    {
        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public Dictionary<string, int> LevelRates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
