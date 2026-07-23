using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;

namespace EasyLotteryDomainTests.Services;

[TestClass]
public sealed class DonateLotteryEngineTests
{
    [TestMethod]
    public void Process_UsesDonationMultiplesAndNeverDrawsBeyondInventory()
    {
        var document = ActiveDocument();
        document.DonateLotteryActivities[0].Prizes[0].Quantity = 1;
        document.DonateLotteryActivities[0].Prizes[0].RemainingQuantity = 1;

        var result = DonateLotteryEngine.Process(document, "payment-1", "Alice", 300m, DateTimeOffset.UtcNow, () => 0d);

        Assert.IsTrue(result.Processed);
        Assert.AreEqual(1, result.Wins.Count);
        Assert.AreEqual(0, document.DonateLotteryActivities[0].Prizes[0].RemainingQuantity);
        Assert.AreEqual(3, result.Wins[0].DrawCount);
    }

    [TestMethod]
    public void Process_RejectsDuplicatePaymentBeforeChangingInventory()
    {
        var document = ActiveDocument();
        DonateLotteryEngine.Process(document, "payment-1", "Alice", 100m, DateTimeOffset.UtcNow, () => 0d);

        var duplicate = DonateLotteryEngine.Process(document, "payment-1", "Alice", 100m, DateTimeOffset.UtcNow, () => 0d);

        Assert.IsFalse(duplicate.Processed);
        Assert.AreEqual(1, document.DonateLotteryDrawRecords.Count);
    }

    [TestMethod]
    public void Process_SkipsInactiveOrOutOfWindowActivities()
    {
        var document = ActiveDocument();
        document.DonateLotteryActivities[0].IsEnabled = false;

        var result = DonateLotteryEngine.Process(document, "payment-1", "Alice", 100m, DateTimeOffset.UtcNow, () => 0d);

        Assert.IsTrue(result.Processed);
        Assert.AreEqual(0, result.Wins.Count);
    }

    private static EasyLotteryConfigDocument ActiveDocument() => new()
    {
        DonateLotteryActivities =
        [
            new DonateLotteryActivity
            {
                Id = 1,
                Name = "拍立得",
                Type = DonateLotteryActivityType.Polaroid,
                MinimumDonationAmount = 100m,
                StartsAtUtc = DateTimeOffset.UtcNow.AddHours(-1),
                EndsAtUtc = DateTimeOffset.UtcNow.AddHours(1),
                IsEnabled = true,
                WinProbability = 1m,
                Prizes = [new DonateLotteryPrize { Id = 1, Name = "獎項", RemainingQuantity = 2, Quantity = 2 }]
            }
        ]
    };
}
