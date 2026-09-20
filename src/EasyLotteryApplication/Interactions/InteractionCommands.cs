using EasyLotteryDomain.Models.Interactions;

namespace EasyLotteryApplication.Interactions;

public sealed record ApplyInteractionEventCommand(
    InteractionEvent InteractionEvent,
    AudienceProfile AudienceProfile,
    PlatformIdentity PlatformIdentity,
    InteractionRound InteractionRound,
    InteractionRoundState CurrentState);
