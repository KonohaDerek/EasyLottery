using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Security;

namespace EasyLotteryCardHub.Endpoints;

public static class SiteRegistrationEndpoints
{
    public static RouteGroupBuilder MapSiteRegistrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/hub/v1/streamer/sites").RequireRateLimiting("streamer-management").AddEndpointFilter(async (context, next) =>
        {
            var environment = context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var ip = context.HttpContext.Connection.RemoteIpAddress;
            if (!context.HttpContext.Request.IsHttps && !(environment.IsDevelopment() && (ip is null || System.Net.IPAddress.IsLoopback(ip))))
                return Results.BadRequest(new { error = "https_required" });

            var authorization = context.HttpContext.Request.Headers.Authorization.ToString();
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                || !context.HttpContext.RequestServices.GetRequiredService<SiteAccessTokenIssuer>()
                    .TryValidateStreamerToken(authorization[7..].Trim(), out _, out _))
                return Results.Unauthorized();
            return await next(context);
        });

        group.MapPost("", async (HttpContext context, RegisterSiteRequest request, PostgresCardHubStore store, SiteAccessTokenIssuer tokens, CancellationToken cancellationToken) =>
        {
            if (!TryReadTenantAndAccount(context, tokens, out var tenantId, out var accountId)) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120
                || !Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
                return Results.BadRequest(new { error = "site_invalid" });
            try
            {
                var site = await store.CreateSiteAsync(tenantId, request.Name.Trim(), uri.GetLeftPart(UriPartial.Path).TrimEnd('/'), cancellationToken);
                var credential = await store.CreateCredentialAsync(site.Id, cancellationToken);
                return Results.Created($"/hub/v1/streamer/sites/{site.Id:D}", new { site, credential });
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = "active_tenant_not_found" });
            }
        });

        group.MapPost("/{siteId:guid}/credentials/rotate", async (Guid siteId, HttpContext context, PostgresCardHubStore store, SiteAccessTokenIssuer tokens, CancellationToken cancellationToken) =>
        {
            if (!TryReadTenantAndAccount(context, tokens, out var tenantId, out _)) return Results.Unauthorized();
            try { return Results.Ok(await store.RotateCredentialAsync(siteId, cancellationToken, tenantId)); }
            catch (KeyNotFoundException) { return Results.NotFound(new { error = "site_not_found" }); }
        });

        group.MapPost("/{siteId:guid}/credentials/{credentialId:guid}/revoke", async (Guid siteId, Guid credentialId, HttpContext context, PostgresCardHubStore store, SiteAccessTokenIssuer tokens, CancellationToken cancellationToken) =>
        {
            if (!TryReadTenantAndAccount(context, tokens, out var tenantId, out _)) return Results.Unauthorized();
            return await store.RevokeCredentialAsync(siteId, credentialId, cancellationToken, tenantId)
                ? Results.NoContent()
                : Results.NotFound(new { error = "credential_not_found" });
        });
        return group;
    }

    private static bool TryReadTenantAndAccount(HttpContext context, SiteAccessTokenIssuer tokens, out Guid tenantId, out Guid accountId)
    {
        tenantId = Guid.Empty;
        accountId = Guid.Empty;
        var authorization = context.Request.Headers.Authorization.ToString();
        return authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            && tokens.TryValidateStreamerToken(authorization[7..].Trim(), out tenantId, out accountId);
    }

    public sealed record RegisterSiteRequest(string Name, string BaseUrl);
}
