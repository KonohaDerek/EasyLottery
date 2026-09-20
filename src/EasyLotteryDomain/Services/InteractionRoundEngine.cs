using EasyLotteryDomain.Models.Interactions;

namespace EasyLotteryDomain.Services;

public sealed class InteractionRoundEngine
{
    public InteractionDecision Apply(InteractionRound round, InteractionRoundState state, Guid audienceProfileId, InteractionEvent interactionEvent)
    {
        if (round.Status != InteractionRoundStatus.Live)
        {
            return InteractionDecision.Reject("round-not-live", audienceProfileId, round.Id, round.Status, state);
        }

        if (state.ProcessedEventKeys.Contains(interactionEvent.EventKey))
        {
            return InteractionDecision.Reject("duplicate", audienceProfileId, round.Id, round.Status, state);
        }

        return round.Type switch
        {
            InteractionRoundType.Vote => ApplyVote(round, state, audienceProfileId, interactionEvent),
            InteractionRoundType.Quiz => ApplyQuiz(round, state, audienceProfileId, interactionEvent),
            InteractionRoundType.SignIn => ApplySignIn(round, state, audienceProfileId, interactionEvent),
            _ => InteractionDecision.Reject("unsupported-round", audienceProfileId, round.Id, round.Status, state)
        };
    }

    private static InteractionDecision ApplyVote(InteractionRound round, InteractionRoundState state, Guid profileId, InteractionEvent interactionEvent)
    {
        if (state.VotesByProfile.ContainsKey(profileId))
        {
            return InteractionDecision.Reject("already-voted", profileId, round.Id, round.Status, state);
        }

        var option = CommandArgument(round.CommandPrefix, interactionEvent.Content);
        if (option is null || !round.VoteOptions.Contains(option, StringComparer.OrdinalIgnoreCase))
        {
            return InteractionDecision.Reject("invalid-vote", profileId, round.Id, round.Status, state);
        }

        var votes = state.VotesByProfile.ToDictionary(pair => pair.Key, pair => pair.Value);
        votes[profileId] = option;
        var next = WithEvent(state, interactionEvent.EventKey) with { VotesByProfile = votes };
        return InteractionDecision.Allow(profileId, round.Id, "vote", round.Status, next, voteOption: option);
    }

    private static InteractionDecision ApplyQuiz(InteractionRound round, InteractionRoundState state, Guid profileId, InteractionEvent interactionEvent)
    {
        if (state.FirstCorrectProfileId is not null)
        {
            return InteractionDecision.Reject("already-answered", profileId, round.Id, round.Status, state);
        }

        var answer = CommandArgument(round.CommandPrefix, interactionEvent.Content);
        if (!string.Equals(answer, round.CorrectAnswer, StringComparison.OrdinalIgnoreCase))
        {
            return InteractionDecision.Reject("incorrect-answer", profileId, round.Id, round.Status, state);
        }

        var points = state.PointsByProfile.ToDictionary(pair => pair.Key, pair => pair.Value);
        points[profileId] = points.GetValueOrDefault(profileId) + round.CorrectAnswerPoints;
        var next = WithEvent(state, interactionEvent.EventKey) with { PointsByProfile = points, FirstCorrectProfileId = profileId };
        return InteractionDecision.Allow(profileId, round.Id, "quiz", round.Status, next, pointDelta: round.CorrectAnswerPoints);
    }

    private static InteractionDecision ApplySignIn(InteractionRound round, InteractionRoundState state, Guid profileId, InteractionEvent interactionEvent)
    {
        if (state.JoinedProfiles.Contains(profileId))
        {
            return InteractionDecision.Reject("already-joined", profileId, round.Id, round.Status, state);
        }

        if (CommandArgument(round.CommandPrefix, interactionEvent.Content) is not "")
        {
            return InteractionDecision.Reject("invalid-command", profileId, round.Id, round.Status, state);
        }

        var joined = state.JoinedProfiles.ToHashSet();
        joined.Add(profileId);
        var next = WithEvent(state, interactionEvent.EventKey) with { JoinedProfiles = joined };
        return InteractionDecision.Allow(profileId, round.Id, "sign-in", round.Status, next, eligibilityTickets: round.EligibilityTickets);
    }

    private static InteractionRoundState WithEvent(InteractionRoundState state, string eventKey) => new()
    {
        VotesByProfile = state.VotesByProfile,
        PointsByProfile = state.PointsByProfile,
        JoinedProfiles = state.JoinedProfiles,
        ProcessedEventKeys = state.ProcessedEventKeys.Append(eventKey).ToHashSet(StringComparer.Ordinal),
        FirstCorrectProfileId = state.FirstCorrectProfileId
    };

    private static string? CommandArgument(string prefix, string content)
    {
        if (!content.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var remainder = content[prefix.Length..];
        return remainder.Length == 0 ? "" : remainder.StartsWith(' ') ? remainder.Trim() : null;
    }
}
