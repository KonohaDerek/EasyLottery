using EasyLotteryDomain.Models.Config;

namespace EasyLotteryDomain.Services
{
    public static class OvertimeSessionTimeCalculator
    {
        public static string DescribeElapsed(DateTimeOffset? startedAtUtc, DateTimeOffset nowUtc)
        {
            if (!IsValidUtcDateTime(startedAtUtc))
            {
                return "尚未開始";
            }

            var diff = nowUtc - startedAtUtc!.Value.ToUniversalTime();
            if (diff < TimeSpan.Zero)
            {
                return $"距離開始還有 {FormatDuration(-diff)}";
            }

            return $"已開台 {FormatDuration(diff)}";
        }

        public static string DescribeRemaining(DateTimeOffset? plannedEndAtUtc, DateTimeOffset nowUtc)
        {
            if (!IsValidUtcDateTime(plannedEndAtUtc))
            {
                return "未設定";
            }

            var diff = plannedEndAtUtc!.Value.ToUniversalTime() - nowUtc;
            if (diff < TimeSpan.Zero)
            {
                return $"已超過 {FormatDuration(-diff)}";
            }

            return $"倒數 {FormatDuration(diff)}";
        }

        public static string FormatDuration(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                duration = duration.Negate();
            }

            duration = duration.Duration();

            var days = duration.Days;
            var hours = duration.Hours;
            var minutes = duration.Minutes;
            var seconds = duration.Seconds;

            return $"{days:00}天 {hours:00}時 {minutes:00}分 {seconds:00} 秒";
        }

        public static DateTimeOffset? ExtendPlannedEnd(
            DateTimeOffset? plannedEndAtUtc,
            OvertimeRewardRule? rule,
            DateTimeOffset nowUtc)
        {
            if (rule == null)
            {
                return plannedEndAtUtc;
            }

            var addition = TimeSpan.FromHours(Math.Max(0, rule.AddHours)) + TimeSpan.FromMinutes(Math.Max(0, rule.AddMinutes));
            if (addition <= TimeSpan.Zero)
            {
                return plannedEndAtUtc;
            }

            var baseline = IsValidUtcDateTime(plannedEndAtUtc)
                ? plannedEndAtUtc!.Value.ToUniversalTime()
                : nowUtc;

            if (baseline < nowUtc)
            {
                baseline = nowUtc;
            }

            return baseline + addition;
        }

        public static double? CalculateProgressPercent(
            DateTimeOffset? startedAtUtc,
            DateTimeOffset? plannedEndAtUtc,
            DateTimeOffset nowUtc)
        {
            if (!IsValidUtcDateTime(startedAtUtc) || !IsValidUtcDateTime(plannedEndAtUtc))
            {
                return null;
            }

            var start = startedAtUtc!.Value.ToUniversalTime();
            var end = plannedEndAtUtc!.Value.ToUniversalTime();
            if (end <= start)
            {
                return null;
            }

            var total = end - start;
            var elapsed = nowUtc - start;
            var percent = elapsed.TotalMilliseconds / total.TotalMilliseconds;
            if (double.IsNaN(percent) || double.IsInfinity(percent))
            {
                return null;
            }

            return Math.Clamp(percent, 0d, 1d) * 100d;
        }

        public static bool IsEventVisible(
            DateTimeOffset occurredAtUtc,
            DateTimeOffset nowUtc,
            int visibleSeconds)
        {
            var seconds = Math.Clamp(visibleSeconds, 1, 60);
            var age = nowUtc.ToUniversalTime() - occurredAtUtc.ToUniversalTime();
            return age >= TimeSpan.Zero && age <= TimeSpan.FromSeconds(seconds);
        }

        private static bool IsValidUtcDateTime(DateTimeOffset? value)
        {
            if (!value.HasValue)
            {
                return false;
            }

            var utcValue = value.Value.ToUniversalTime();
            return utcValue.Year >= 2000;
        }
    }
}
