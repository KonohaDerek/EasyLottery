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

    [TestMethod]
    public void Process_UsesPrizeProbabilityAndStoresThankYouForTheRemainingChance()
    {
        var document = ActiveDocument();
        document.DonateLotteryActivities[0].Prizes[0].Probability = 35m;

        var miss = DonateLotteryEngine.Process(document, "payment-miss", "Alice", 100m, DateTimeOffset.UtcNow, () => .5d);
        var win = DonateLotteryEngine.Process(document, "payment-win", "Alice", 100m, DateTimeOffset.UtcNow, () => .2d);

        Assert.AreEqual(0, miss.Wins.Count);
        Assert.AreEqual("銘謝惠顧", document.DonateLotteryDrawRecords[0].PrizeName);
        Assert.IsFalse(document.DonateLotteryDrawRecords[0].IsWinning);
        Assert.AreEqual(1, win.Wins.Count);
        Assert.AreEqual("獎項", win.Wins[0].PrizeName);
    }

    [TestMethod]
    public void Process_PersistsDonateNotificationDetails()
    {
        var document = ActiveDocument();
        var result = DonateLotteryEngine.Process(document, "payment-details", "Alice", 100m, DateTimeOffset.UtcNow, () => 0d, "加油！", "測試付款");

        Assert.AreEqual("加油！", result.Wins[0].DonationMessage);
        Assert.AreEqual("測試付款", result.Wins[0].PaymentMethod);
    }

    [TestMethod]
    public void Process_TestTargetCanBypassActivityEligibility()
    {
        var document = ActiveDocument();
        document.DonateLotteryActivities[0].IsEnabled = false;
        document.DonateLotteryActivities[0].StartsAtUtc = DateTimeOffset.UtcNow.AddDays(1);

        var result = DonateLotteryEngine.Process(document, "obs-test", "Alice", 100m, DateTimeOffset.UtcNow, () => 0d,
            targetActivityId: document.DonateLotteryActivities[0].Id, bypassActivityEligibility: true);

        Assert.AreEqual(1, result.Wins.Count);
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
                Prizes = [new DonateLotteryPrize { Id = 1, Name = "獎項", RemainingQuantity = 2, Quantity = 2, Probability = 100m }]
            }
        ]
    };
}
