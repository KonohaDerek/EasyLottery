namespace EasyLotteryDomain.Models.Config;

/// <summary>Immutable evidence for one core lottery result and its draw-time input snapshot.</summary>
public sealed class CoreLotteryDrawRecord
{
    public Guid DrawId { get; set; }
    public DateTimeOffset DrawnAtUtc { get; set; }
    public string WinnerParticipantId { get; set; } = "";
    public string WinnerDisplayName { get; set; } = "";
    public string PrizeName { get; set; } = "";
    public CoreLotteryDrawRuleSnapshot RuleSnapshot { get; set; } = new();
    public List<CoreLotteryDrawParticipantSnapshot> Participants { get; set; } = [];
    /// <summary>Locked participant-list version the draw used; null for unlocked draws and legacy history.</summary>
    public CoreLotteryDrawPoolSnapshotVersion? PoolSnapshotVersion { get; set; }
}

/// <summary>
/// Identifies the locked pool version by its lock-time hash and lock time. Admin-only history data; the hash is an
/// integrity marker, not a security boundary.
/// </summary>
public sealed class CoreLotteryDrawPoolSnapshotVersion
{
    public string LockHash { get; set; } = "";
    public DateTimeOffset LockedAtUtc { get; set; }
}

/// <summary>Only the existing lottery inputs needed to identify a participant entry.</summary>
public sealed class CoreLotteryDrawParticipantSnapshot
{
    public string ParticipantId { get; set; } = "";
    public string LotteryGroupId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public CoreLotteryDrawExternalIdentity? ExternalIdentity { get; set; }
    /// <summary>Null for manual entries and for history saved before interaction provenance existed.</summary>
    public CoreLotteryDrawInteractionSource? InteractionSource { get; set; }
    public string Level { get; set; } = "";
    public string Group { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public string EligibilityStatus { get; set; } = "eligible";
}

/// <summary>Existing external platform/account identity only; never a credential.</summary>
public sealed class CoreLotteryDrawExternalIdentity
{
    public string Platform { get; set; } = "";
    public string AccountId { get; set; } = "";
}

/// <summary>
/// Immutable interaction provenance copied at import time. Later profile merges, unlinks or renames never rewrite it.
/// Admin-only history data; never projected to public OBS responses.
/// </summary>
public sealed class CoreLotteryDrawInteractionSource
{
    public Guid RoundId { get; set; }
    public Guid SnapshotId { get; set; }
    public Guid AudienceProfileId { get; set; }
}

/// <summary>Versioned rules and prize order that determined the saved draw.</summary>
public sealed class CoreLotteryDrawRuleSnapshot
{
    public string AlgorithmVersion { get; set; } = "";
    public string EligibilityPolicyVersion { get; set; } = "";
    public string GroupWinnerPolicyVersion { get; set; } = "";
    public int EligibleEntryCount { get; set; }
    public string PrizeSelectionPolicyVersion { get; set; } = "";
    public List<string> PrizeQueue { get; set; } = [];
}
