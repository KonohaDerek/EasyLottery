using System.Data;
using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Security;
using Microsoft.EntityFrameworkCore;

namespace EasyLotteryCardHub.Services;

public sealed class InviteRedemptionService(HubDbContext db, TimeProvider timeProvider)
{
    public async Task<StreamerTenant> RedeemAsync(
        string inviteTokenHash,
        string? displayName,
        string? slug,
        byte[] credentialId,
        byte[] publicKey,
        byte[] userHandle,
        int signatureCounter,
        CancellationToken cancellationToken)
    {
        var normalizedDisplayName = HubValidation.NormalizeDisplayName(displayName);
        var normalizedSlug = HubValidation.NormalizeSlug(slug);
        var now = timeProvider.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        var claimed = await db.StreamerInvites
            .Where(invite => invite.TokenHash == inviteTokenHash && invite.RedeemedAt == null && invite.ExpiresAt > now)
            .ExecuteUpdateAsync(update => update.SetProperty(invite => invite.RedeemedAt, now), cancellationToken);
        if (claimed != 1)
            throw new HubServiceException(StatusCodes.Status410Gone, "invite_invalid_or_expired", "The invite is invalid, expired, or has already been redeemed.");

        var invite = await db.StreamerInvites.SingleAsync(item => item.TokenHash == inviteTokenHash, cancellationToken);
        var tenant = new StreamerTenant
        {
            Id = Guid.NewGuid(),
            Email = invite.Email,
            EmailNormalized = invite.EmailNormalized,
            DisplayName = normalizedDisplayName,
            Slug = normalizedSlug,
            Status = TenantStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        invite.RedeemedByTenantId = tenant.Id;
        db.Streamers.Add(tenant);
        db.StreamerPasskeys.Add(new StreamerPasskey
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            CredentialId = credentialId,
            PublicKey = publicKey,
            UserHandle = userHandle,
            SignatureCounter = Math.Max(0, signatureCounter),
            Name = "Streamer Passkey",
            CreatedAt = now
        });
        HubAuditWriter.Append(db, timeProvider, "streamer.registered", "streamer", tenant.Id.ToString("D"), tenant.Id,
            new { inviteId = invite.Id, tenantId = tenant.Id, slug = tenant.Slug });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return tenant;
    }

    public static string HashInviteToken(string token) => SecretHasher.Hash(token);
}
