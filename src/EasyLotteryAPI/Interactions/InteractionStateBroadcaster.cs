using EasyLotteryApplication.Settings;
using Microsoft.AspNetCore.SignalR;

namespace EasyLotteryApi.Interactions;

public sealed record InteractionRoundSnapshot(Guid Id, string Name, string Status, string Type, IReadOnlyList<string> VoteOptions, IReadOnlyDictionary<string, int> VoteCounts, IReadOnlyList<InteractionLeaderboardEntry> Leaderboard);
public sealed record InteractionLeaderboardEntry(Guid AudienceProfileId, string DisplayName, int Points);

public sealed class InteractionStateBroadcaster(IInteractionsYamlDocumentRepository repository, IHubContext<InteractionHub> hub)
{
    public async Task<InteractionRoundSnapshot?> GetSnapshotAsync(Guid roundId, CancellationToken cancellationToken = default)
    {
        var document = await repository.ReadAsync(cancellationToken);
        var round = document.Rounds.SingleOrDefault(item => item.Id == roundId);
        if (round is null) return null;
        var voteCounts = round.State.VotesByProfile.Values.GroupBy(value => value, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var leaderboard = round.State.PointsByProfile.OrderByDescending(item => item.Value).ThenBy(item => item.Key).Take(10).Select(item => new InteractionLeaderboardEntry(item.Key, document.AudienceProfiles.SingleOrDefault(profile => profile.Id == item.Key)?.DisplayName ?? "觀眾", item.Value)).ToList();
        return new InteractionRoundSnapshot(round.Id, round.Name, round.Status.ToString(), round.Type.ToString(), round.VoteOptions, voteCounts, leaderboard);
    }

    public async Task PublishAsync(Guid roundId, CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(roundId, cancellationToken);
        if (snapshot is not null) await hub.Clients.Group(InteractionRoundGroups.For(roundId)).SendAsync(InteractionHub.StateChangedEvent, snapshot, cancellationToken);
    }
}
