using EasyLotteryApplication.Interactions;
using EasyLotteryDomain.Models.Interactions;
using EasyLotteryInfrastructure.Settings;

namespace EasyLotteryInfrastructure.Interactions;

public sealed class YamlInteractionRepository(YamlInteractionsDocumentRepository documents) : IInteractionRepository
{
    public Task<InteractionsYamlDocument> ReadAsync(CancellationToken cancellationToken = default) =>
        documents.ReadAsync(cancellationToken);

    public Task<bool> ApplyAsync(
        InteractionEvent interactionEvent,
        InteractionDecision decision,
        AudienceProfile profile,
        PlatformIdentity identity,
        InteractionRound round,
        CancellationToken cancellationToken = default)
    {
        if (decision.AudienceProfileId != profile.Id || identity.AudienceProfileId != profile.Id || decision.RoundId != round.Id)
        {
            throw new InvalidOperationException("互動判定必須使用同一觀眾與回合。");
        }

        return documents.MutateAsync(document =>
        {
            if (document.ProcessedEventKeys.Contains(interactionEvent.EventKey, StringComparer.Ordinal))
            {
                return false;
            }

            Upsert(document.AudienceProfiles, profile, item => item.Id);
            Upsert(document.PlatformIdentities, identity, item => item.Id);
            Upsert(document.Rounds, round, item => item.Id);
            document.ProcessedEventKeys.Add(interactionEvent.EventKey);
            return true;
        }, cancellationToken);
    }

    private static void Upsert<T>(List<T> items, T item, Func<T, Guid> id) where T : class
    {
        var index = items.FindIndex(existing => id(existing) == id(item));
        if (index < 0) items.Add(item);
        else items[index] = item;
    }
}
