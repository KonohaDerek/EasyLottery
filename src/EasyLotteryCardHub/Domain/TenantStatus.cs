namespace EasyLotteryCardHub.Domain;

public enum TenantStatus
{
    Active,
    Disabled,
    PendingDeletion,
    Deleted
}

public static class TenantStatusPolicy
{
    public static bool CanTransition(TenantStatus current, TenantStatus target, DateTimeOffset? pendingSince, DateTimeOffset now)
    {
        if (current == TenantStatus.Deleted) return false;
        if (target != TenantStatus.Deleted) return true;
        return current == TenantStatus.PendingDeletion && pendingSince is { } since && now - since >= TimeSpan.FromDays(30);
    }

    public static bool CanPublish(this TenantStatus status) => status == TenantStatus.Active;
    public static bool CanDraw(this TenantStatus status) => status == TenantStatus.Active;
    public static bool CanTrade(this TenantStatus status) => status == TenantStatus.Active;
    // Deleted tenants retain the holder's name/serial/rarity history and show a removed placeholder.
    public static bool CanViewExistingCards(this TenantStatus status) => true;
}
