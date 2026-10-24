using EasyLotteryDomain.Models.Config;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Extensions.Options;

namespace EasyLotteryInfrastructure.Settings;

public sealed record CoreLotteryDrawWriteResult(CoreLotteryDrawRecord Record, bool IsNew);

public sealed class CoreLotteryDrawIdConflictException(Guid drawId)
    : InvalidOperationException($"抽獎識別碼 {drawId:D} 已被不同結果使用。")
{
    public Guid DrawId { get; } = drawId;
}

/// <summary>Persists core draw results through the existing YAML or SQLite config storage.</summary>
public sealed class CoreLotteryDrawHistoryRepository(
    IOptions<StorageProviderOptions> options,
    YamlActivityResultsDocumentRepository yamlResults,
    SqliteConfigStore sqliteStore,
    TimeProvider timeProvider)
{
    public Task<CoreLotteryDrawWriteResult> SaveAsync(
        Guid drawId,
        string winnerParticipantId,
        CoreLotteryDrawRuleSnapshot ruleSnapshot,
        IReadOnlyList<CoreLotteryDrawParticipantSnapshot> participants,
        CancellationToken cancellationToken = default)
    {
        if (drawId == Guid.Empty || string.IsNullOrWhiteSpace(winnerParticipantId)
            || ruleSnapshot is null || participants is null)
            throw new ArgumentException("抽獎識別碼、得獎籤、規則與完整名單不可空白。");

        var normalizedRules = CopyRules(ruleSnapshot);
        var normalizedParticipants = participants.Select(CopyParticipant).ToList();
        if (normalizedParticipants.Count(participant => participant.ParticipantId == winnerParticipantId) != 1)
            throw new ArgumentException("得獎籤不存在於抽獎名單快照中。");
        var winner = normalizedParticipants.Single(participant => participant.ParticipantId == winnerParticipantId);
        if (winner.EligibilityStatus != "eligible" || normalizedRules.EligibleEntryCount != normalizedParticipants.Count(participant => participant.EligibilityStatus == "eligible")
            || normalizedRules.PrizeQueue.Count == 0)
            throw new ArgumentException("抽獎結果與規則快照不一致。");

        if (options.Value.NormalizedProvider == "sqlite")
        {
            return sqliteStore.MutateSettingsAsync(document => SaveToList(
                document.CoreLotteryDrawRecords ??= [], drawId, winnerParticipantId, normalizedRules, normalizedParticipants), cancellationToken);
        }

        return yamlResults.MutateAsync(document => SaveToList(
            document.CoreLotteryDrawRecords ??= [], drawId, winnerParticipantId, normalizedRules, normalizedParticipants), cancellationToken);
    }

    public async Task<CoreLotteryDrawRecord?> FindAsync(Guid drawId, CancellationToken cancellationToken = default)
    {
        var records = await LoadAllAsync(cancellationToken);
        return records.FirstOrDefault(record => record.DrawId == drawId);
    }

    public async Task<IReadOnlyList<CoreLotteryDrawRecord>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        if (options.Value.NormalizedProvider == "sqlite")
        {
            var document = await sqliteStore.LoadAsync(cancellationToken);
            return document.CoreLotteryDrawRecords ?? [];
        }

        var resultDocument = await yamlResults.ReadAsync(cancellationToken);
        return resultDocument.CoreLotteryDrawRecords ?? [];
    }

    private CoreLotteryDrawWriteResult SaveToList(
        List<CoreLotteryDrawRecord> records,
        Guid drawId,
        string winnerParticipantId,
        CoreLotteryDrawRuleSnapshot ruleSnapshot,
        List<CoreLotteryDrawParticipantSnapshot> participants)
    {
        var existing = records.FirstOrDefault(record => record.DrawId == drawId);
        if (existing is not null)
        {
            if (HasSameOutcome(existing, winnerParticipantId, ruleSnapshot, participants))
                return new CoreLotteryDrawWriteResult(existing, IsNew: false);
            throw new CoreLotteryDrawIdConflictException(drawId);
        }

        var winner = participants.Single(participant => participant.ParticipantId == winnerParticipantId);

        var record = new CoreLotteryDrawRecord
        {
            DrawId = drawId,
            DrawnAtUtc = timeProvider.GetUtcNow(),
            WinnerParticipantId = winnerParticipantId,
            WinnerDisplayName = winner.DisplayName,
            PrizeName = ruleSnapshot.PrizeQueue[0],
            RuleSnapshot = ruleSnapshot,
            Participants = participants
        };
        records.Add(record);
        return new CoreLotteryDrawWriteResult(record, IsNew: true);
    }

    private static bool HasSameOutcome(
        CoreLotteryDrawRecord existing,
        string winnerParticipantId,
        CoreLotteryDrawRuleSnapshot rules,
        IReadOnlyList<CoreLotteryDrawParticipantSnapshot> participants) =>
        existing.WinnerParticipantId == winnerParticipantId
        && existing.WinnerDisplayName == participants.FirstOrDefault(participant => participant.ParticipantId == winnerParticipantId)?.DisplayName
        && existing.PrizeName == rules.PrizeQueue.FirstOrDefault()
        && existing.RuleSnapshot is not null
        && SameRules(existing.RuleSnapshot, rules)
        && existing.Participants is not null
        && existing.Participants.Count == participants.Count
        && existing.Participants.Zip(participants).All(pair => SameParticipant(pair.First, pair.Second));

    private static bool SameRules(CoreLotteryDrawRuleSnapshot left, CoreLotteryDrawRuleSnapshot right) =>
        left.AlgorithmVersion == right.AlgorithmVersion
        && left.EligibilityPolicyVersion == right.EligibilityPolicyVersion
        && left.GroupWinnerPolicyVersion == right.GroupWinnerPolicyVersion
        && left.EligibleEntryCount == right.EligibleEntryCount
        && left.PrizeSelectionPolicyVersion == right.PrizeSelectionPolicyVersion
        && left.PrizeQueue.SequenceEqual(right.PrizeQueue, StringComparer.Ordinal);

    private static bool SameParticipant(CoreLotteryDrawParticipantSnapshot left, CoreLotteryDrawParticipantSnapshot right) =>
        left.ParticipantId == right.ParticipantId
        && left.LotteryGroupId == right.LotteryGroupId
        && left.DisplayName == right.DisplayName
        && left.Level == right.Level
        && left.Group == right.Group
        && left.EligibilityStatus == right.EligibilityStatus
        && left.Tags.SequenceEqual(right.Tags, StringComparer.Ordinal)
        && ((left.ExternalIdentity is null && right.ExternalIdentity is null)
            || (left.ExternalIdentity is not null && right.ExternalIdentity is not null
                && left.ExternalIdentity.Platform == right.ExternalIdentity.Platform
                && left.ExternalIdentity.AccountId == right.ExternalIdentity.AccountId));

    private static CoreLotteryDrawRuleSnapshot CopyRules(CoreLotteryDrawRuleSnapshot value) => new()
    {
        AlgorithmVersion = value.AlgorithmVersion,
        EligibilityPolicyVersion = value.EligibilityPolicyVersion,
        GroupWinnerPolicyVersion = value.GroupWinnerPolicyVersion,
        EligibleEntryCount = value.EligibleEntryCount,
        PrizeSelectionPolicyVersion = value.PrizeSelectionPolicyVersion,
        PrizeQueue = value.PrizeQueue is null ? [] : [.. value.PrizeQueue]
    };

    private static CoreLotteryDrawParticipantSnapshot CopyParticipant(CoreLotteryDrawParticipantSnapshot value) => new()
    {
        ParticipantId = value.ParticipantId,
        LotteryGroupId = value.LotteryGroupId,
        DisplayName = value.DisplayName,
        ExternalIdentity = value.ExternalIdentity is null ? null : new CoreLotteryDrawExternalIdentity
        {
            Platform = value.ExternalIdentity.Platform,
            AccountId = value.ExternalIdentity.AccountId
        },
        Level = value.Level,
        Group = value.Group,
        Tags = value.Tags is null ? [] : [.. value.Tags],
        EligibilityStatus = value.EligibilityStatus
    };
}
