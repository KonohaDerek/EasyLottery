using EasyLotteryDomain.Services;
using EasyLotteryDomain.Models.Config;

namespace EasyLotteryDomainTests.Services
{
    [TestClass]
    public sealed class OvertimeSessionTimeCalculatorTests
    {
        [TestMethod]
        public void DescribeElapsed_RejectsAncientDates()
        {
            var result = OvertimeSessionTimeCalculator.DescribeElapsed(
                new DateTimeOffset(1, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero));

            Assert.AreEqual("尚未開始", result);
        }

        [TestMethod]
        public void DescribeRemaining_RejectsAncientDates()
        {
            var result = OvertimeSessionTimeCalculator.DescribeRemaining(
                new DateTimeOffset(1, 1, 1, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero));

            Assert.AreEqual("未設定", result);
        }

        [TestMethod]
        public void FormatDuration_UsesDayHourMinuteSecondLayout()
        {
            var result = OvertimeSessionTimeCalculator.FormatDuration(
                new TimeSpan(days: 2, hours: 3, minutes: 4, seconds: 5));

            Assert.AreEqual("02天 03時 04分 05 秒", result);
        }

        [TestMethod]
        public void DescribeRemaining_PrefixesCountdownLabel()
        {
            var now = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero);
            var end = new DateTimeOffset(2026, 3, 30, 15, 4, 5, TimeSpan.Zero);

            var result = OvertimeSessionTimeCalculator.DescribeRemaining(end, now);

            Assert.AreEqual("倒數 02天 03時 04分 05 秒", result);
        }

        [TestMethod]
        public void ExtendPlannedEnd_AddsRewardDuration()
        {
            var rule = new OvertimeRewardRule
            {
                AddHours = 1,
                AddMinutes = 30
            };

            var nowUtc = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero);
            var plannedEnd = new DateTimeOffset(2026, 3, 28, 13, 0, 0, TimeSpan.Zero);

            var result = OvertimeSessionTimeCalculator.ExtendPlannedEnd(plannedEnd, rule, nowUtc);

            Assert.IsNotNull(result);
            Assert.AreEqual(new DateTimeOffset(2026, 3, 28, 14, 30, 0, TimeSpan.Zero), result);
        }

        [TestMethod]
        public void CalculateProgressPercent_ReturnsExpectedValue()
        {
            var start = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero);
            var end = new DateTimeOffset(2026, 3, 28, 13, 0, 0, TimeSpan.Zero);
            var now = new DateTimeOffset(2026, 3, 28, 12, 30, 0, TimeSpan.Zero);

            var result = OvertimeSessionTimeCalculator.CalculateProgressPercent(start, end, now);

            Assert.IsNotNull(result);
            Assert.AreEqual(50d, result.Value, 0.001d);
        }

        [TestMethod]
        public void IsEventVisible_RespectsConfiguredSeconds()
        {
            var occurredAt = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero);

            var visible = OvertimeSessionTimeCalculator.IsEventVisible(
                occurredAt,
                new DateTimeOffset(2026, 3, 28, 12, 0, 7, TimeSpan.Zero),
                8);

            var hidden = OvertimeSessionTimeCalculator.IsEventVisible(
                occurredAt,
                new DateTimeOffset(2026, 3, 28, 12, 0, 9, TimeSpan.Zero),
                8);

            Assert.IsTrue(visible);
            Assert.IsFalse(hidden);
        }
    }
}
