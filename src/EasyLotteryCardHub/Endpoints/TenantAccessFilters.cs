using System.Security.Claims;
using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Security;
using EasyLotteryCardHub.Services;
using Microsoft.EntityFrameworkCore;

namespace EasyLotteryCardHub.Endpoints;

public sealed class SiteTenantStatusFilter(HubDbContext db, TimeProvider timeProvider) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var user = context.HttpContext.User;
        if (!TryClaimGuid(user, "tenant_id", out var tenantId)
            || !TryClaimGuid(user, "site_id", out var siteId)
            || !TryClaimGuid(user, "site_credential_id", out var credentialId))
            return HubErrorResults.Unauthorized("invalid_site_token", "The site token is invalid.");

        var tenantStatus = await db.Streamers.Where(item => item.Id == tenantId).Select(item => (TenantStatus?)item.Status).SingleOrDefaultAsync(context.HttpContext.RequestAborted);
        if (tenantStatus is null) return HubErrorResults.Forbidden("tenant_unavailable", "This tenant is unavailable.");
        if (tenantStatus != TenantStatus.Active) return TenantAccessErrors.ForStatus(tenantStatus.Value);

        var now = timeProvider.GetUtcNow();
        var validCredential = await db.SiteCredentials
            .Join(db.StreamerSites, credential => credential.SiteId, site => site.Id, (credential, site) => new { credential, site })
            .AnyAsync(item => item.credential.Id == credentialId
                && item.site.Id == siteId
                && item.site.TenantId == tenantId
                && item.credential.RevokedAt == null
                && (item.credential.ExpiresAt == null || item.credential.ExpiresAt > now), context.HttpContext.RequestAborted);
        if (!validCredential) return HubErrorResults.Unauthorized("site_credential_revoked", "The site credential is no longer valid.");

        return await next(context);
    }

    private static bool TryClaimGuid(ClaimsPrincipal principal, string name, out Guid value) =>
        Guid.TryParse(principal.FindFirstValue(name), out value);
}

public sealed class StreamerTenantStatusFilter(HubDbContext db) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var idClaim = context.HttpContext.User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(idClaim, out var tenantId))
            return HubErrorResults.Unauthorized("invalid_streamer_token", "The streamer token is invalid.");

        var status = await db.Streamers.Where(item => item.Id == tenantId).Select(item => (TenantStatus?)item.Status).SingleOrDefaultAsync(context.HttpContext.RequestAborted);
        if (status is null) return HubErrorResults.Forbidden("tenant_unavailable", "This tenant is unavailable.");
        if (status != TenantStatus.Active) return TenantAccessErrors.ForStatus(status.Value);

        return await next(context);
    }
}
