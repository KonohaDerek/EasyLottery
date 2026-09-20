using EasyLotteryDomain.Models.Interactions;

namespace EasyLotteryApplication.Interactions;

public interface IInteractionRepository
{
    Task<InteractionsYamlDocument> ReadAsync(CancellationToken cancellationToken = default);

    Task<bool> ApplyAsync(
        InteractionEvent interactionEvent,
        InteractionDecision decision,
        AudienceProfile profile,
        PlatformIdentity identity,
        InteractionRound round,
        CancellationToken cancellationToken = default);
}
