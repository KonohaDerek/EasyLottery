using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Security;
using System.Net;

namespace EasyLotteryCardHub.Endpoints;

public static class SiteOAuthEndpoints
{
    public static RouteGroupBuilder MapSiteOAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/hub/v1/sites").RequireRateLimiting("site-token");
        group.MapPost("/token", async (HttpRequest request, PostgresCardHubStore store, SiteAccessTokenIssuer tokens, CancellationToken cancellationToken) =>
        {
            var environment = request.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var remoteIp = request.HttpContext.Connection.RemoteIpAddress;
            var localDevelopmentRequest = environment.IsDevelopment() && (remoteIp is null || IPAddress.IsLoopback(remoteIp));
            if (!request.IsHttps && !localDevelopmentRequest)
                return Results.BadRequest(new { error = "https_required" });

            if (!request.HasFormContentType)
                return Results.BadRequest(new { error = "invalid_request" });
            var form = await request.ReadFormAsync(cancellationToken);
            if (!string.Equals(form["grant_type"], "client_credentials", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(form["client_id"])
                || string.IsNullOrWhiteSpace(form["client_secret"]))
                return Results.BadRequest(new { error = "invalid_request" });

            var site = await store.AuthenticateSiteAsync(form["client_id"]!, form["client_secret"]!, cancellationToken);
            if (site is null) return Results.Json(new { error = "invalid_client" }, statusCode: StatusCodes.Status401Unauthorized);
            var (token, expiresIn) = tokens.Issue(site.TenantId, site.SiteId, site.ClientId);
            return Results.Ok(new { access_token = token, token_type = "Bearer", expires_in = expiresIn, tenant_id = site.TenantId, tenant_name = site.TenantName });
        }).Accepts<IFormCollection>("application/x-www-form-urlencoded");
        return group;
    }
}
