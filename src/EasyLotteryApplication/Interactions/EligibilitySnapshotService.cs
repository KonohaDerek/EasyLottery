using EasyLotteryDomain.Models.Interactions;
using EasyLotteryApplication.Settings;

namespace EasyLotteryApplication.Interactions;

/// <summary>
/// Admin-only platform account currently linked to an imported AudienceProfile; never a credential.
/// Platform + ChannelScope + AccountId mirror PlatformIdentity.EventScopeKey; clients must match all three to link.
/// </summary>
public sealed record EligibilityIdentity(string Platform, string AccountId, string ChannelScope = "");

/// <summary>Stable profile identity for one eligibility candidate. DisplayName is display data only, never a key.</summary>
public sealed record EligibilityProfileEntry(Guid AudienceProfileId, string DisplayName, IReadOnlyList<EligibilityIdentity> Identities);

/// <summary>
/// ProfileIds/EligibleCount keep the original live-candidate meaning. Entries are exactly what an import returns:
/// the persisted snapshot once imported, otherwise the live candidates.
/// </summary>
public sealed record EligibilityPreview(
    Guid RoundId,
    int EligibleCount,
    int ExistingSnapshotCount,
    bool Imported,
    IReadOnlyList<Guid> ProfileIds,
    Guid? SnapshotId = null,
    string RoundName = "",
    string RoundStatus = "",
    DateTimeOffset? SnapshotCreatedAtUtc = null,
    IReadOnlyList<EligibilityProfileEntry>? Entries = null);

public sealed class EligibilitySnapshotService(IInteractionsYamlDocumentRepository repository)
{
    private readonly SemaphoreSlim _importGate = new(1, 1);

    public async Task<EligibilityPreview> PreviewAsync(Guid roundId, CancellationToken cancellationToken = default)
    {
        var document = await repository.ReadAsync(cancellationToken);
        var round = document.Rounds.SingleOrDefault(item => item.Id == roundId) ?? throw new KeyNotFoundException("找不到互動回合。");
        var ids = LiveCandidateIds(round);
        var snapshot = round.EligibilitySnapshot is { Imported: true } imported ? imported : null;
        return new EligibilityPreview(roundId, ids.Count, round.EligibilitySnapshot?.AudienceProfileIds.Count ?? 0, round.EligibilitySnapshot?.Imported ?? false, ids,
            snapshot?.Id, round.Name, round.Status.ToString(), snapshot?.CreatedAtUtc, Project(document, snapshot?.AudienceProfileIds ?? ids));
    }

    public async Task<EligibilityPreview> ImportAsync(Guid roundId, CancellationToken cancellationToken = default)
    {
        // Serialize read-modify-write so concurrent retries cannot persist two different snapshot IDs.
        await _importGate.WaitAsync(cancellationToken);
        try
        {
            var document = await repository.ReadAsync(cancellationToken);
            var round = document.Rounds.SingleOrDefault(item => item.Id == roundId) ?? throw new KeyNotFoundException("找不到互動回合。");
            if (round.EligibilitySnapshot?.Imported == true) return FromSnapshot(document, round, round.EligibilitySnapshot);
            if (round.Status == InteractionRoundStatus.Cancelled) throw new InvalidOperationException("已取消的互動回合不可匯入抽獎資格。");
            var ids = LiveCandidateIds(round);
            if (ids.Count == 0) throw new InvalidOperationException("此互動回合目前沒有合格觀眾，未建立資格快照。");
            round.EligibilitySnapshot = new EligibilitySnapshot { RoundId = roundId, AudienceProfileIds = ids, Imported = true };
            await repository.SaveAsync(document, cancellationToken);
            return FromSnapshot(document, round, round.EligibilitySnapshot);
        }
        finally
        {
            _importGate.Release();
        }
    }

    private static List<Guid> LiveCandidateIds(InteractionRound round) =>
        round.State.PointsByProfile.Keys.Concat(round.State.JoinedProfiles).Distinct().ToList();

    private static EligibilityPreview FromSnapshot(InteractionsYamlDocument document, InteractionRound round, EligibilitySnapshot snapshot) =>
        new(round.Id, snapshot.AudienceProfileIds.Count, snapshot.AudienceProfileIds.Count, true, snapshot.AudienceProfileIds,
            snapshot.Id, round.Name, round.Status.ToString(), snapshot.CreatedAtUtc, Project(document, snapshot.AudienceProfileIds));

    private static IReadOnlyList<EligibilityProfileEntry> Project(InteractionsYamlDocument document, IReadOnlyList<Guid> profileIds) =>
        profileIds.Select(id =>
        {
            var identities = document.PlatformIdentities
                .Where(identity => identity.AudienceProfileId == id && !string.IsNullOrWhiteSpace(identity.ExternalUserId))
                .OrderBy(identity => identity.Platform)
                .ThenBy(identity => identity.ChannelScope, StringComparer.Ordinal)
                .ThenBy(identity => identity.ExternalUserId, StringComparer.Ordinal)
                .ToList();
            var displayName = document.AudienceProfiles.FirstOrDefault(profile => profile.Id == id)?.DisplayName;
            if (string.IsNullOrWhiteSpace(displayName)) displayName = identities.Select(identity => identity.DisplayName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
            return new EligibilityProfileEntry(
                id,
                displayName?.Trim() ?? "",
                identities.Select(identity => new EligibilityIdentity(
                        identity.Platform.ToString().ToLowerInvariant(), identity.ExternalUserId.Trim(), identity.ChannelScope?.Trim() ?? ""))
                    .DistinctBy(identity => (identity.Platform, identity.ChannelScope, identity.AccountId)).ToList());
        }).ToList();
}
