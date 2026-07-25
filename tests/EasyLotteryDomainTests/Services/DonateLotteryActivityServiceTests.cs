using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryDomainTests.Helpers;

namespace EasyLotteryDomainTests.Services;

[TestClass]
public sealed class DonateLotteryActivityServiceTests
{
    [TestMethod]
    public void SaveAsync_RejectsPrizeProbabilitiesOverOneHundredPercent()
    {
        var service = new DonateLotteryActivityService(new InMemoryEasyLotteryConfigStore());
        var activity = new DonateLotteryActivity
        {
            Name = "測試活動",
            StartsAtUtc = DateTimeOffset.UtcNow,
            EndsAtUtc = DateTimeOffset.UtcNow.AddDays(1),
            Prizes =
            [
                new DonateLotteryPrize { Name = "A", Probability = 60m },
                new DonateLotteryPrize { Name = "B", Probability = 41m }
            ]
        };

        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => service.SaveAsync(activity).GetAwaiter().GetResult());

        StringAssert.Contains(exception.Message, "不可超過 100%");
    }
}
