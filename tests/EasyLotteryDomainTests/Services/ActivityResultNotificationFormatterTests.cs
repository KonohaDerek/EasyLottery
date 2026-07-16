using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;

namespace EasyLotteryDomainTests.Services
{
    [TestClass]
    public class ActivityResultNotificationFormatterTests
    {
        [TestMethod]
        public void BuildBody_IncludesActivitySummaryAndItems()
        {
            var record = new ActivityResultRecord
            {
                ActivityName = "拍立得抽獎",
                ActivityDateUtc = new DateTime(2026, 7, 16, 12, 30, 0, DateTimeKind.Utc),
                Summary = "中獎項目：A 獎",
                Items =
                [
                    new ActivityResultItem { Order = 2, Name = "B 獎", Description = "第二項" },
                    new ActivityResultItem { Order = 1, Name = "A 獎", Description = "第一項" }
                ]
            };

            var body = ActivityResultNotificationFormatter.BuildBody(record);

            Assert.IsTrue(body.Contains("EasyLottery 結果通知"));
            Assert.IsTrue(body.Contains("活動名稱：拍立得抽獎"));
            Assert.IsTrue(body.Contains("摘要：中獎項目：A 獎"));
            Assert.IsTrue(body.Contains("1. A 獎 - 第一項"));
            Assert.IsTrue(body.Contains("2. B 獎 - 第二項"));
        }
    }
}