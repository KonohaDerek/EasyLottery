using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;

namespace EasyLotteryDomainTests.Services
{
    [TestClass]
    public sealed class OvertimeRewardRuleCalculatorTests
    {
        [TestMethod]
        public void DefaultSettings_ContainCommonRewardRules()
        {
            var settings = new OvertimeOverlaySettings();

            Assert.AreEqual(3, settings.RewardRules.Count);
            Assert.AreEqual("入門加班", settings.RewardRules[0].Label);
            Assert.AreEqual(100m, settings.RewardRules[0].AmountThreshold);
            Assert.AreEqual(10, settings.RewardRules[0].AddMinutes);
        }

        [TestMethod]
        public void FindMatchedRule_PicksHighestEligibleThreshold()
        {
            var rules = new List<OvertimeRewardRule>
            {
                new() { AmountThreshold = 100, AddMinutes = 10 },
                new() { AmountThreshold = 300, AddMinutes = 30 },
                new() { AmountThreshold = 500, AddHours = 1 }
            };

            var matched = OvertimeRewardRuleCalculator.FindMatchedRule(rules, 450m);

            Assert.IsNotNull(matched);
            Assert.AreEqual(300m, matched.AmountThreshold);
        }

        [TestMethod]
        public void DescribeDuration_FormatsHoursAndMinutes()
        {
            Assert.AreEqual("30 分鐘", OvertimeRewardRuleCalculator.DescribeDuration(0, 30));
            Assert.AreEqual("1 小時", OvertimeRewardRuleCalculator.DescribeDuration(1, 0));
            Assert.AreEqual("1 小時 30 分鐘", OvertimeRewardRuleCalculator.DescribeDuration(1, 30));
        }
    }
}
