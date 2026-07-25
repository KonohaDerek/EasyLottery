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

    [TestMethod]
    public async Task DeleteAsync_RemovesActivityButPreservesDrawHistory()
    {
        var store = new InMemoryEasyLotteryConfigStore();
        var document = await store.LoadAsync();
        document.DonateLotteryActivities.Add(new DonateLotteryActivity { Id = 7, Name = "待刪除活動" });
        document.DonateLotteryDrawRecords.Add(new DonateLotteryDrawRecord { Id = 3, ActivityId = 7, PrizeName = "歷史獎項" });
        await store.SaveAsync(document);
        var service = new DonateLotteryActivityService(store);

        await service.DeleteAsync(7);

        var saved = await store.LoadAsync();
        Assert.IsFalse(saved.DonateLotteryActivities.Any(activity => activity.Id == 7));
        Assert.IsTrue(saved.DonateLotteryDrawRecords.Any(record => record.ActivityId == 7));
    }
}
