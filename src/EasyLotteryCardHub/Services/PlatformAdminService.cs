using EasyLotteryCardHub.Data;
using Microsoft.EntityFrameworkCore;

namespace EasyLotteryCardHub.Services;

public sealed class PlatformAdminService(HubDbContext db, TimeProvider timeProvider)
{
    public async Task<CreatedInvite> CreateInviteAsync(Guid adminId, string? email, int? expiresInHours, CancellationToken cancellationToken)
    {
        var normalizedEmail = HubValidation.NormalizeEmail(email);
        var hours = expiresInHours ?? 48;
        if (hours is < 1 or > 168)
            throw new HubServiceException(StatusCodes.Status400BadRequest, "invalid_invite_lifetime", "Invite expiry must be between 1 and 168 hours.");

        var token = Security.SecretHasher.CreateInviteToken();
        var now = timeProvider.GetUtcNow();
        var invite = new StreamerInvite
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            EmailNormalized = normalizedEmail,
            TokenHash = Security.SecretHasher.Hash(token),
            CreatedByPlatformAdminId = adminId,
            CreatedAt = now,
            ExpiresAt = now.AddHours(hours)
        };
        db.StreamerInvites.Add(invite);
        HubAuditWriter.Append(db, timeProvider, "streamer_invite.created", "platform_admin", adminId.ToString("D"), null,
            new { inviteId = invite.Id, expiresAt = invite.ExpiresAt });
        await db.SaveChangesAsync(cancellationToken);
        return new CreatedInvite(invite.Id, token, invite.ExpiresAt);
    }

    public async Task<TenantStatus> SetTenantStatusAsync(Guid adminId, Guid tenantId, TenantStatus status, CancellationToken cancellationToken)
    {
        var tenant = await db.Streamers.SingleOrDefaultAsync(item => item.Id == tenantId, cancellationToken)
            ?? throw new HubServiceException(StatusCodes.Status404NotFound, "tenant_not_found", "Tenant was not found.");

        if (tenant.Status == TenantStatus.Deleted && status != TenantStatus.Deleted)
            throw new HubServiceException(StatusCodes.Status409Conflict, "tenant_deleted", "A deleted tenant cannot be reactivated.");

        var before = tenant.Status;
        var now = timeProvider.GetUtcNow();
        tenant.Status = status;
        tenant.UpdatedAt = now;
        if (status == TenantStatus.PendingDeletion && before != TenantStatus.PendingDeletion)
            tenant.PendingDeletionAt = now;
        else if (status is TenantStatus.Active or TenantStatus.Disabled)
            tenant.PendingDeletionAt = null;
        HubAuditWriter.Append(db, timeProvider, "tenant.status_changed", "platform_admin", adminId.ToString("D"), tenant.Id,
            new { previousStatus = before.ToStorageValue(), status = status.ToStorageValue() });
        await db.SaveChangesAsync(cancellationToken);
        return tenant.Status;
    }
}

public sealed record CreatedInvite(Guid Id, string Token, DateTimeOffset ExpiresAt);
