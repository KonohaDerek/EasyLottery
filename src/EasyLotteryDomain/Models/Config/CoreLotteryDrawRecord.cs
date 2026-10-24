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
}

/// <summary>Only the existing lottery inputs needed to identify a participant entry.</summary>
public sealed class CoreLotteryDrawParticipantSnapshot
{
    public string ParticipantId { get; set; } = "";
    public string LotteryGroupId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public CoreLotteryDrawExternalIdentity? ExternalIdentity { get; set; }
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
