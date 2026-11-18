using System.Data;
using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Security;
using Microsoft.EntityFrameworkCore;

namespace EasyLotteryCardHub.Services;

public sealed class SiteCredentialService(HubDbContext db, HubTokenService tokens, TimeProvider timeProvider)
{
    public static readonly TimeSpan PreviousSecretOverlap = TimeSpan.FromHours(24);

    public async Task<RegisteredSite> RegisterSiteAsync(
        Guid tenantId,
        string? name,
        string? baseUrl,
        bool allowLoopbackHttp,
        CancellationToken cancellationToken)
    {
        var normalizedName = HubValidation.NormalizeSiteName(name);
        var normalizedUrl = HubValidation.NormalizeSiteUrl(baseUrl, allowLoopbackHttp);
        var site = new StreamerSite
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = normalizedName,
            BaseUrl = normalizedUrl,
            ClientId = SecretHasher.CreateClientId(),
            CreatedAt = timeProvider.GetUtcNow()
        };
        var secret = SecretHasher.CreateSiteSecret();
        var credential = NewCredential(site.Id, secret);
        db.StreamerSites.Add(site);
        db.SiteCredentials.Add(credential);
        HubAuditWriter.Append(db, timeProvider, "streamer_site.registered", "streamer", tenantId.ToString("D"), tenantId,
            new { siteId = site.Id, site.ClientId, site.BaseUrl, credentialId = credential.Id });
        await db.SaveChangesAsync(cancellationToken);
        return new RegisteredSite(site.Id, site.Name, site.BaseUrl, site.ClientId, secret, credential.Id);
    }

    public async Task<RegisteredSiteCredential> RotateAsync(Guid tenantId, Guid siteId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var site = await db.StreamerSites.SingleOrDefaultAsync(item => item.Id == siteId && item.TenantId == tenantId, cancellationToken)
            ?? throw new HubServiceException(StatusCodes.Status404NotFound, "site_not_found", "Site was not found.");
        var activeCredentials = await db.SiteCredentials
            .Where(item => item.SiteId == siteId && item.RevokedAt == null && (item.ExpiresAt == null || item.ExpiresAt > now))
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        if (activeCredentials.Any(item => item.ExpiresAt is not null))
            throw new HubServiceException(StatusCodes.Status409Conflict, "rotation_in_progress", "The previous site secret remains valid for 24 hours; wait for it to expire before rotating again.");

        foreach (var current in activeCredentials)
            current.ExpiresAt = now.Add(PreviousSecretOverlap);

        var secret = SecretHasher.CreateSiteSecret();
        var credential = NewCredential(siteId, secret);
        db.SiteCredentials.Add(credential);
        HubAuditWriter.Append(db, timeProvider, "site_credential.rotated", "streamer", tenantId.ToString("D"), tenantId,
            new { siteId, previousCredentialIds = activeCredentials.Select(item => item.Id), credentialId = credential.Id, previousExpiresAt = now.Add(PreviousSecretOverlap) });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RegisteredSiteCredential(site.ClientId, credential.Id, secret);
    }

    public async Task<bool> RevokeAsync(Guid tenantId, Guid siteId, Guid credentialId, CancellationToken cancellationToken)
    {
        var credential = await db.SiteCredentials
            .Join(db.StreamerSites, key => key.SiteId, site => site.Id, (key, site) => new { key, site })
            .Where(item => item.key.Id == credentialId && item.key.SiteId == siteId && item.site.TenantId == tenantId)
            .Select(item => item.key)
            .SingleOrDefaultAsync(cancellationToken);
        if (credential is null) return false;
        if (credential.RevokedAt is not null) return true;

        var now = timeProvider.GetUtcNow();
        credential.RevokedAt = now;
        HubAuditWriter.Append(db, timeProvider, "site_credential.revoked", "streamer", tenantId.ToString("D"), tenantId,
            new { siteId, credentialId, revokedAt = now });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IssuedHubToken> ExchangeClientCredentialsAsync(string clientId, string secret, CancellationToken cancellationToken)
    {
        var candidates = await db.SiteCredentials
            .Join(db.StreamerSites, credential => credential.SiteId, site => site.Id, (credential, site) => new { credential, site })
            .Where(item => item.site.ClientId == clientId)
            .ToListAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        SiteCredential? matched = null;
        StreamerSite? matchedSite = null;
        foreach (var item in candidates)
        {
            var secretMatches = SecretHasher.Verify(secret, item.credential.SecretHash);
            var isValid = item.credential.RevokedAt is null && (item.credential.ExpiresAt is null || item.credential.ExpiresAt > now);
            if (secretMatches && isValid)
            {
                matched = item.credential;
                matchedSite = item.site;
            }
        }

        if (matched is null || matchedSite is null)
        {
            // Keep the same hash work for unknown client IDs and invalid secrets.
            _ = SecretHasher.Verify(secret, new string('0', 64));
            throw new HubServiceException(StatusCodes.Status401Unauthorized, "invalid_client", "Client authentication failed.");
        }

        var tenant = await db.Streamers.SingleOrDefaultAsync(item => item.Id == matchedSite.TenantId, cancellationToken);
        if (tenant is null)
            throw new HubServiceException(StatusCodes.Status401Unauthorized, "invalid_client", "Client authentication failed.");
        if (tenant.Status != TenantStatus.Active)
            throw TenantAccessErrors.AsException(tenant.Status);

        return tokens.IssueSiteToken(tenant.Id, matchedSite.Id, matched.Id);
    }

    private SiteCredential NewCredential(Guid siteId, string secret) => new()
    {
        Id = Guid.NewGuid(),
        SiteId = siteId,
        SecretHash = SecretHasher.Hash(secret),
        CreatedAt = timeProvider.GetUtcNow()
    };
}

public sealed record RegisteredSite(Guid Id, string Name, string BaseUrl, string ClientId, string ClientSecret, Guid CredentialId);
public sealed record RegisteredSiteCredential(string ClientId, Guid CredentialId, string ClientSecret);
