using EasyLotteryDomain.Models.Interactions;
using EasyLotteryDomain.Services;

namespace EasyLotteryDomainTests.Interactions;

[TestClass]
public sealed class InteractionRoundEngineTests
{
    [TestMethod]
    public void Vote_RejectsSecondLinkedIdentityForSameProfile()
    {
        var engine = new InteractionRoundEngine();
        var round = LiveRound(InteractionRoundType.Vote, "!vote", ["blue", "red"]);
        var profileId = Guid.NewGuid();
        var state = InteractionRoundState.Empty;

        var first = engine.Apply(round, state, profileId, Event(PlatformKind.YouTube, "event-1", "!vote blue"));
        var second = engine.Apply(round, first.NextState, profileId, Event(PlatformKind.Twitch, "event-2", "!vote red"));

        Assert.IsTrue(first.Accepted);
        Assert.AreEqual("blue", first.VoteOption);
        Assert.IsFalse(second.Accepted);
        Assert.AreEqual("already-voted", second.Reason);
    }

    [TestMethod]
    public void Replay_RejectsDuplicateExternalEventWithoutScoreChange()
    {
        var engine = new InteractionRoundEngine();
        var round = LiveRound(InteractionRoundType.Quiz, "!answer", correctAnswer: "42", correctAnswerPoints: 10);
        var profileId = Guid.NewGuid();
        var first = engine.Apply(round, InteractionRoundState.Empty, profileId, Event(PlatformKind.YouTube, "event-1", "!answer 42"));

        var replay = engine.Apply(round, first.NextState, profileId, Event(PlatformKind.YouTube, "event-1", "!answer 42"));

        Assert.IsTrue(first.Accepted);
        Assert.AreEqual(10, first.PointDelta);
        Assert.IsFalse(replay.Accepted);
        Assert.AreEqual("duplicate", replay.Reason);
        Assert.AreEqual(10, replay.NextState.PointsByProfile[profileId]);
    }

    [TestMethod]
    public void Quiz_AcceptsOnlyFirstCorrectAnswer()
    {
        var engine = new InteractionRoundEngine();
        var round = LiveRound(InteractionRoundType.Quiz, "!answer", correctAnswer: "blue", correctAnswerPoints: 7);
        var firstProfile = Guid.NewGuid();
        var first = engine.Apply(round, InteractionRoundState.Empty, firstProfile, Event(PlatformKind.YouTube, "event-1", "!answer blue"));

        var later = engine.Apply(round, first.NextState, Guid.NewGuid(), Event(PlatformKind.Twitch, "event-2", "!answer blue"));

        Assert.IsTrue(first.Accepted);
        Assert.AreEqual(7, first.PointDelta);
        Assert.IsFalse(later.Accepted);
        Assert.AreEqual("already-answered", later.Reason);
    }

    [TestMethod]
    public void SignIn_GrantsConfiguredTicketOnlyOncePerRound()
    {
        var engine = new InteractionRoundEngine();
        var round = LiveRound(InteractionRoundType.SignIn, "!join", eligibilityTickets: 2);
        var profileId = Guid.NewGuid();
        var first = engine.Apply(round, InteractionRoundState.Empty, profileId, Event(PlatformKind.YouTube, "event-1", "!join"));

        var later = engine.Apply(round, first.NextState, profileId, Event(PlatformKind.Twitch, "event-2", "!join"));

        Assert.IsTrue(first.Accepted);
        Assert.AreEqual(2, first.EligibilityTickets);
        Assert.IsFalse(later.Accepted);
        Assert.AreEqual("already-joined", later.Reason);
    }

    [TestMethod]
    public void PausedAndSettledRounds_RejectAudienceEvents()
    {
        var engine = new InteractionRoundEngine();
        foreach (var status in new[] { InteractionRoundStatus.Paused, InteractionRoundStatus.Settled })
        {
            var round = LiveRound(InteractionRoundType.SignIn, "!join");
            round.Status = status;

            var decision = engine.Apply(round, InteractionRoundState.Empty, Guid.NewGuid(), Event(PlatformKind.YouTube, "event-1", "!join"));

            Assert.IsFalse(decision.Accepted);
            Assert.AreEqual("round-not-live", decision.Reason);
            Assert.AreEqual(status, decision.RoundState);
        }
    }

    private static InteractionRound LiveRound(
        InteractionRoundType type,
        string commandPrefix,
        string[]? voteOptions = null,
        string? correctAnswer = null,
        int correctAnswerPoints = 0,
        int eligibilityTickets = 0) => new()
        {
            Id = Guid.NewGuid(),
            Name = "Test round",
            Type = type,
            Status = InteractionRoundStatus.Live,
            CommandPrefix = commandPrefix,
            VoteOptions = voteOptions?.ToList() ?? [],
            CorrectAnswer = correctAnswer ?? "",
            CorrectAnswerPoints = correctAnswerPoints,
            EligibilityTickets = eligibilityTickets
        };

    private static InteractionEvent Event(PlatformKind platform, string eventId, string content) =>
        new(platform, "channel-abc", eventId, $"{platform}-viewer", content);
}
