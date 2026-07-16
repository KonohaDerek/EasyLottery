using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Models.Overtime;

namespace EasyLotteryDomain.Services
{
    public static class OvertimeRewardRuleCalculator
    {
        public static OvertimeRewardRule? FindMatchedRule(IEnumerable<OvertimeRewardRule>? rules, decimal? amount)
        {
            if (!amount.HasValue || rules == null)
            {
                return null;
            }

            return rules
                .Where(rule => rule.IsEnabled)
                .Where(rule => amount.Value >= rule.AmountThreshold)
                .OrderByDescending(rule => rule.AmountThreshold)
                .ThenByDescending(rule => rule.AddHours * 60 + rule.AddMinutes)
                .FirstOrDefault();
        }

        public static string DescribeRule(OvertimeRewardRule rule)
        {
            var label = string.IsNullOrWhiteSpace(rule.Label) ? "加班規則" : rule.Label.Trim();
            var amountText = FormatAmount(rule.AmountThreshold);
            var durationText = DescribeDuration(rule.AddHours, rule.AddMinutes);
            return $"{label}：滿 {amountText} 加 {durationText}";
        }

        public static string DescribeDuration(int addHours, int addMinutes)
        {
            var totalMinutes = Math.Max(0, addHours) * 60 + Math.Max(0, addMinutes);
            if (totalMinutes <= 0)
            {
                return "0 分鐘";
            }

            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;

            if (hours > 0 && minutes > 0)
            {
                return $"{hours} 小時 {minutes} 分鐘";
            }

            if (hours > 0)
            {
                return $"{hours} 小時";
            }

            return $"{minutes} 分鐘";
        }

        public static string DescribeReward(OvertimeRewardRule rule)
        {
            return $"加 {DescribeDuration(rule.AddHours, rule.AddMinutes)}";
        }

        private static string FormatAmount(decimal amount)
        {
            return $"NT${amount:0.##}";
        }
    }
}
