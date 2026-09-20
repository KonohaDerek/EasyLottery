using EasyLotteryDomain.Models.Interactions;
using EasyLotteryApplication.Settings;

namespace EasyLotteryApplication.Interactions;

public sealed record EligibilityPreview(Guid RoundId, int EligibleCount, int ExistingSnapshotCount, bool Imported, IReadOnlyList<Guid> ProfileIds);

public sealed class EligibilitySnapshotService(IInteractionsYamlDocumentRepository repository)
{
    public async Task<EligibilityPreview> PreviewAsync(Guid roundId, CancellationToken cancellationToken = default)
    {
        var document = await repository.ReadAsync(cancellationToken);
        var round = document.Rounds.SingleOrDefault(item => item.Id == roundId) ?? throw new KeyNotFoundException("找不到互動回合。");
        var ids = round.State.PointsByProfile.Keys.Concat(round.State.JoinedProfiles).Distinct().ToList();
        return new EligibilityPreview(roundId, ids.Count, round.EligibilitySnapshot?.AudienceProfileIds.Count ?? 0, round.EligibilitySnapshot?.Imported ?? false, ids);
    }

    public async Task<EligibilityPreview> ImportAsync(Guid roundId, CancellationToken cancellationToken = default)
    {
        var document = await repository.ReadAsync(cancellationToken);
        var round = document.Rounds.SingleOrDefault(item => item.Id == roundId) ?? throw new KeyNotFoundException("找不到互動回合。");
        if (round.EligibilitySnapshot?.Imported == true) return new EligibilityPreview(roundId, round.EligibilitySnapshot.AudienceProfileIds.Count, round.EligibilitySnapshot.AudienceProfileIds.Count, true, round.EligibilitySnapshot.AudienceProfileIds);
        var ids = round.State.PointsByProfile.Keys.Concat(round.State.JoinedProfiles).Distinct().ToList();
        round.EligibilitySnapshot = new EligibilitySnapshot { RoundId = roundId, AudienceProfileIds = ids, Imported = true };
        await repository.SaveAsync(document, cancellationToken);
        return new EligibilityPreview(roundId, ids.Count, ids.Count, true, ids);
    }
}
