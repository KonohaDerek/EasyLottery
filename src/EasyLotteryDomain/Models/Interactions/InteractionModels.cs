using EasyLotteryDomain.Models.Config;

namespace EasyLotteryDomain.Models.Interactions;

public enum PlatformKind
{
    YouTube,
    Twitch
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
    private InteractionDecision(Guid audienceProfileId, Guid roundId, string kind)
    {
        AudienceProfileId = audienceProfileId;
        RoundId = roundId;
        Kind = kind;
    }

    public Guid AudienceProfileId { get; }
    public Guid RoundId { get; }
    public string Kind { get; }

    public static InteractionDecision Accepted(Guid audienceProfileId, Guid roundId, string kind) =>
        new(audienceProfileId, roundId, kind);
}

public sealed class InteractionsYamlDocument : IYamlVersionedDocument
{
    public int ConfigVersion { get; set; } = YamlDocumentSchema.CurrentVersion;
    public List<AudienceProfile> AudienceProfiles { get; set; } = [];
    public List<PlatformIdentity> PlatformIdentities { get; set; } = [];
    public List<InteractionRound> Rounds { get; set; } = [];
    public List<string> ProcessedEventKeys { get; set; } = [];
}
