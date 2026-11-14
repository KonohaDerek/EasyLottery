namespace EasyLotteryCardHub.Data;

public enum TenantStatus
{
    Active,
    Disabled,
    PendingDeletion,
    Deleted
}

public static class TenantStatuses
{
    public static string ToStorageValue(this TenantStatus status) => status switch
    {
        TenantStatus.Active => "active",
        TenantStatus.Disabled => "disabled",
        TenantStatus.PendingDeletion => "pending_deletion",
        TenantStatus.Deleted => "deleted",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    public static bool TryParse(string? value, out TenantStatus status)
    {
        status = value?.Trim().ToLowerInvariant() switch
        {
            "active" => TenantStatus.Active,
            "disabled" => TenantStatus.Disabled,
            "pending_deletion" => TenantStatus.PendingDeletion,
            "deleted" => TenantStatus.Deleted,
            _ => (TenantStatus)(-1)
        };
        return Enum.IsDefined(status);
    }
}

public sealed class StreamerTenant
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string EmailNormalized { get; set; }
    public required string DisplayName { get; set; }
    public required string Slug { get; set; }
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PendingDeletionAt { get; set; }
}

public sealed class StreamerPasskey
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required byte[] CredentialId { get; set; }
    public required byte[] PublicKey { get; set; }
    public required byte[] UserHandle { get; set; }
    public int SignatureCounter { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class StreamerSite
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Name { get; set; }
    public required string BaseUrl { get; set; }
    public required string ClientId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class SiteCredential
{
    public Guid Id { get; set; }
    public Guid SiteId { get; set; }
    public required string SecretHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class StreamerInvite
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string EmailNormalized { get; set; }
    public required string TokenHash { get; set; }
    public Guid CreatedByPlatformAdminId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RedeemedAt { get; set; }
    public Guid? RedeemedByTenantId { get; set; }
}

public sealed class PlatformAdmin
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string EmailNormalized { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DisabledAt { get; set; }
}

public sealed class AuditEvent
{
    public long Id { get; set; }
    public required string EventType { get; set; }
    public required string ActorType { get; set; }
    public required string ActorId { get; set; }
    public Guid? TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public required string DetailsJson { get; set; }
}
