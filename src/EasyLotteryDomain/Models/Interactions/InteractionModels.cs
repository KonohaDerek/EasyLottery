using EasyLotteryDomain.Models.Config;

namespace EasyLotteryDomain.Models.Interactions;

public enum PlatformKind
{
    YouTube,
    Twitch
}

public enum InteractionRoundType
{
    SignIn,
    Vote,
    Quiz
}

public enum InteractionRoundStatus
{
    Draft,
    Scheduled,
    Live,
    Paused,
    Settled,
    Cancelled
}

public sealed class AudienceProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PlatformIdentity
{
    public PlatformIdentity()
    {
    }

    public PlatformIdentity(PlatformKind platform, string externalUserId, string channelScope, Guid audienceProfileId, string displayName)
    {
        Platform = platform;
        ExternalUserId = externalUserId;
        ChannelScope = channelScope;
        AudienceProfileId = audienceProfileId;
        DisplayName = displayName;
    }

    public Guid Id { get; set; } = Guid.NewGuid();
    public PlatformKind Platform { get; set; }
    public string ExternalUserId { get; set; } = "";
    public string ChannelScope { get; set; } = "";
    public Guid AudienceProfileId { get; set; }
    public string DisplayName { get; set; } = "";
    public DateTimeOffset LastSeenAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string EventScopeKey => $"{Platform}:{ChannelScope}:{ExternalUserId}";
}

public sealed class InteractionRound
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public InteractionRoundType Type { get; set; }
    public InteractionRoundStatus Status { get; set; } = InteractionRoundStatus.Draft;
    public string CommandPrefix { get; set; } = "";
    public List<string> VoteOptions { get; set; } = [];
    public string CorrectAnswer { get; set; } = "";
    public int CorrectAnswerPoints { get; set; }
    public int EligibilityTickets { get; set; }
}

public sealed class InteractionEvent
{
    public InteractionEvent(PlatformKind platform, string channelScope, string externalEventId, string externalUserId, string content)
    {
        Platform = platform;
        ChannelScope = channelScope;
        ExternalEventId = externalEventId;
        ExternalUserId = externalUserId;
        Content = content;
    }

    public PlatformKind Platform { get; }
    public string ChannelScope { get; }
    public string ExternalEventId { get; }
    public string ExternalUserId { get; }
    public string Content { get; }
    public string EventKey => $"{Platform}:{ChannelScope}:{ExternalEventId}";
}

public sealed class InteractionDecision
{
    private InteractionDecision(
        bool accepted,
        string reason,
        Guid audienceProfileId,
        Guid roundId,
        string kind,
        int pointDelta,
        string? voteOption,
        int eligibilityTickets,
        InteractionRoundStatus roundState,
        InteractionRoundState nextState)
    {
        Accepted = accepted;
        Reason = reason;
        AudienceProfileId = audienceProfileId;
        RoundId = roundId;
        Kind = kind;
        PointDelta = pointDelta;
        VoteOption = voteOption;
        EligibilityTickets = eligibilityTickets;
        RoundState = roundState;
        NextState = nextState;
    }

    public bool Accepted { get; }
    public string Reason { get; }
    public Guid AudienceProfileId { get; }
    public Guid RoundId { get; }
    public string Kind { get; }
    public int PointDelta { get; }
    public string? VoteOption { get; }
    public int EligibilityTickets { get; }
    public InteractionRoundStatus RoundState { get; }
    public InteractionRoundState NextState { get; }

    public static InteractionDecision Allow(
        Guid audienceProfileId,
        Guid roundId,
        string kind,
        InteractionRoundStatus roundState,
        InteractionRoundState nextState,
        int pointDelta = 0,
        string? voteOption = null,
        int eligibilityTickets = 0) =>
        new(true, "accepted", audienceProfileId, roundId, kind, pointDelta, voteOption, eligibilityTickets, roundState, nextState);

    public static InteractionDecision Reject(
        string reason,
        Guid audienceProfileId,
        Guid roundId,
        InteractionRoundStatus roundState,
        InteractionRoundState nextState) =>
        new(false, reason, audienceProfileId, roundId, "rejected", 0, null, 0, roundState, nextState);
}

public sealed record InteractionRoundState
{
    public static InteractionRoundState Empty { get; } = new();

    public IReadOnlyDictionary<Guid, string> VotesByProfile { get; init; } = new Dictionary<Guid, string>();
    public IReadOnlyDictionary<Guid, int> PointsByProfile { get; init; } = new Dictionary<Guid, int>();
    public IReadOnlySet<Guid> JoinedProfiles { get; init; } = new HashSet<Guid>();
    public IReadOnlySet<string> ProcessedEventKeys { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public Guid? FirstCorrectProfileId { get; init; }
}

public sealed class InteractionsYamlDocument : IYamlVersionedDocument
{
    public int ConfigVersion { get; set; } = YamlDocumentSchema.CurrentVersion;
    public List<AudienceProfile> AudienceProfiles { get; set; } = [];
    public List<PlatformIdentity> PlatformIdentities { get; set; } = [];
    public List<InteractionRound> Rounds { get; set; } = [];
    public List<string> ProcessedEventKeys { get; set; } = [];
}
