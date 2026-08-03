using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;

namespace EasyLotteryDomainTests.Services;

[TestClass]
public sealed class DonateObsRevealLifecycleTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 3, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Advance_InitialHistoryOnlySetsCursorAndKeepsOverlayTransparent()
    {
        var state = new DonateObsRevealState();
        var history = CreateResult(id: 18, isWinning: true);

        var transition = DonateObsRevealLifecycle.Advance(state, CreateActivity(), history, Start);

        Assert.IsFalse(transition.IsNewResult);
        Assert.IsTrue(state.IsInitialized);
        Assert.AreEqual(history.Id, state.ObservedWinId);
        Assert.IsNull(state.CurrentResult);
    }

    [TestMethod]
    public void Advance_WithDonationInformation_UsesNotificationAnimationRevealAndClearSequence()
    {
        var activity = CreateActivity(showDonateInformation: true, animationDurationSeconds: 8, resultDisplayDurationSeconds: 15);
        var state = Initialize(activity);
        var result = CreateResult(id: 2, isWinning: true);

        var newResult = DonateObsRevealLifecycle.Advance(state, activity, result, Start);
        Assert.IsTrue(newResult.IsNewResult);
        Assert.AreEqual(DonateObsRevealPhase.Notification, state.Phase);
        Assert.AreEqual(Start.AddSeconds(3), state.PhaseExpiresAtUtc);

        DonateObsRevealLifecycle.Advance(state, activity, result, Start.AddSeconds(3));
        Assert.AreEqual(DonateObsRevealPhase.Animation, state.Phase);
        Assert.AreEqual(Start.AddSeconds(11), state.PhaseExpiresAtUtc);

        var reveal = DonateObsRevealLifecycle.Advance(state, activity, result, Start.AddSeconds(11));
        Assert.IsTrue(reveal.EnteredReveal);
        Assert.AreEqual(DonateObsRevealPhase.Reveal, state.Phase);
        Assert.AreEqual(Start.AddSeconds(26), state.ResultExpiresAtUtc);

        var cleared = DonateObsRevealLifecycle.Advance(state, activity, result, Start.AddSeconds(26));
        Assert.IsTrue(cleared.Cleared);
        Assert.IsNull(state.CurrentResult);
    }

    [TestMethod]
    public void Advance_WithoutDonationInformation_StartsAnimationAndRevealsMissResult()
    {
        var activity = CreateActivity(showDonateInformation: false, animationDurationSeconds: 12, resultDisplayDurationSeconds: 9);
        var state = Initialize(activity);
        var miss = CreateResult(id: 5, isWinning: false);

        DonateObsRevealLifecycle.Advance(state, activity, miss, Start);
        Assert.AreEqual(DonateObsRevealPhase.Animation, state.Phase);
        Assert.AreEqual(Start.AddSeconds(12), state.PhaseExpiresAtUtc);
        Assert.IsFalse(state.CurrentResult!.IsWinning);

        var reveal = DonateObsRevealLifecycle.Advance(state, activity, miss, Start.AddSeconds(12));
        Assert.IsTrue(reveal.EnteredReveal);
        Assert.AreEqual(DonateObsRevealPhase.Reveal, state.Phase);

        var cleared = DonateObsRevealLifecycle.Advance(state, activity, miss, Start.AddSeconds(21));
        Assert.IsTrue(cleared.Cleared);
        Assert.IsNull(state.CurrentResult);
    }

    private static DonateObsRevealState Initialize(DonateLotteryActivity activity)
    {
        var state = new DonateObsRevealState();
        DonateObsRevealLifecycle.Advance(state, activity, null, Start);
        return state;
    }

    private static DonateLotteryActivity CreateActivity(bool showDonateInformation = true, int animationDurationSeconds = 8, int resultDisplayDurationSeconds = 15) =>
        new()
        {
            Id = 1,
            ShowDonateInformation = showDonateInformation,
            AnimationDurationSeconds = animationDurationSeconds,
            ResultDisplayDurationSeconds = resultDisplayDurationSeconds
        };

    private static DonateLotteryDrawRecord CreateResult(int id, bool isWinning) =>
        new()
        {
            Id = id,
            ActivityId = 1,
            DonorName = "測試贊助者",
            PrizeName = isWinning ? "測試獎項" : "銘謝惠顧",
            IsWinning = isWinning
        };
}
