using System.Security.Claims;
using System.Text;
using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Security;
using EasyLotteryCardHub.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Routing;

namespace EasyLotteryCardHub.Endpoints;

public static class HubEndpoints
{
    public static void MapHubEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", () => Results.Ok(new { service = "EasyLotteryCardHub", status = "running" }));

        app.MapPost("/api/auth/streamer-registration/passkey/options", async (
            RegistrationOptionsRequest request,
            StreamerPasskeyService passkeys,
            CancellationToken cancellationToken) =>
        {
            var result = await passkeys.BeginRegistrationAsync(request.InviteToken, request.DisplayName, request.Slug, cancellationToken);
            return Results.Ok(result);
        }).RequireRateLimiting("authentication");

        app.MapPost("/api/auth/streamer-registration/passkey/verify", async (
            PasskeyVerifyRequest request,
            StreamerPasskeyService passkeys,
            CancellationToken cancellationToken) =>
        {
            var result = await passkeys.FinishRegistrationAsync(request.FlowId, request.Credential, cancellationToken);
            return Results.Ok(new StreamerTokenResponse(result.TenantId, result.Token.AccessToken, result.Token.ExpiresAt));
        }).RequireRateLimiting("authentication");

        app.MapPost("/api/auth/streamer/passkey/options", async (
            StreamerLoginOptionsRequest request,
            StreamerPasskeyService passkeys,
            CancellationToken cancellationToken) =>
            Results.Ok(await passkeys.BeginLoginAsync(request.Email, cancellationToken)))
            .RequireRateLimiting("authentication");

        app.MapPost("/api/auth/streamer/passkey/verify", async (
            PasskeyVerifyRequest request,
            StreamerPasskeyService passkeys,
            CancellationToken cancellationToken) =>
        {
            var token = await passkeys.FinishLoginAsync(request.FlowId, request.Credential, cancellationToken);
            return Results.Ok(new StreamerTokenResponse(null, token.AccessToken, token.ExpiresAt));
        }).RequireRateLimiting("authentication");

        app.MapPost("/oauth/token", async (HttpContext context, SiteCredentialService credentials, TimeProvider timeProvider, CancellationToken cancellationToken) =>
        {
            var form = context.Request.HasFormContentType
                ? await context.Request.ReadFormAsync(cancellationToken)
                : null;
            var grantType = form?["grant_type"].ToString();
            if (!string.Equals(grantType, "client_credentials", StringComparison.Ordinal))
                return OAuthTokenError(StatusCodes.Status400BadRequest, "unsupported_grant_type", "Only the client_credentials grant is supported.");

            var (clientId, clientSecret) = ReadClientCredentials(context.Request, form);
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                context.Response.Headers.WWWAuthenticate = "Basic realm=\"Card Hub\"";
                return OAuthTokenError(StatusCodes.Status401Unauthorized, "invalid_client", "Client authentication failed.");
            }

            IssuedHubToken token;
            try
            {
                token = await credentials.ExchangeClientCredentialsAsync(clientId, clientSecret, cancellationToken);
            }
            catch (HubServiceException exception) when (exception.Code == "invalid_client")
            {
                context.Response.Headers.WWWAuthenticate = "Basic realm=\"Card Hub\"";
                return OAuthTokenError(StatusCodes.Status401Unauthorized, "invalid_client", "Client authentication failed.");
            }
            var expiresIn = Math.Max(1, (int)Math.Ceiling((token.ExpiresAt - timeProvider.GetUtcNow()).TotalSeconds));
            return Results.Ok(new
            {
                access_token = token.AccessToken,
                token_type = "Bearer",
                expires_in = expiresIn
            });
        }).RequireRateLimiting("authentication");

        var admin = app.MapGroup("/api/platform-admin").RequireAuthorization(HubPolicies.PlatformAdmin);
        admin.MapPost("/invites", async (
            CreateInviteRequest request,
            ClaimsPrincipal principal,
            HubDbContext db,
            PlatformAdminService service,
            CancellationToken cancellationToken) =>
        {
            var adminRecord = await FindActiveAdminAsync(principal, db, cancellationToken);
            if (adminRecord is null) return HubErrorResults.Forbidden("platform_admin_inactive", "The platform admin is not active.");
            var invite = await service.CreateInviteAsync(adminRecord.Id, request.Email, request.ExpiresInHours, cancellationToken);
            return Results.Created($"/api/platform-admin/invites/{invite.Id:D}", new CreatedInviteResponse(invite.Id, HubValidation.NormalizeEmail(request.Email), invite.Token, invite.ExpiresAt));
        }).RequireRateLimiting("admin-sensitive");

        admin.MapPatch("/tenants/{tenantId:guid}/status", async (
            Guid tenantId,
            SetTenantStatusRequest request,
            ClaimsPrincipal principal,
            HubDbContext db,
            PlatformAdminService service,
            CancellationToken cancellationToken) =>
        {
            var adminRecord = await FindActiveAdminAsync(principal, db, cancellationToken);
            if (adminRecord is null) return HubErrorResults.Forbidden("platform_admin_inactive", "The platform admin is not active.");
            if (!TenantStatuses.TryParse(request.Status, out var status))
                return HubErrorResults.Create(StatusCodes.Status400BadRequest, "invalid_tenant_status", "Status must be active, disabled, pending_deletion, or deleted.");
            var updated = await service.SetTenantStatusAsync(adminRecord.Id, tenantId, status, cancellationToken);
            return Results.Ok(new TenantStatusResponse(tenantId, updated.ToStorageValue()));
        }).RequireRateLimiting("admin-sensitive");

        var streamers = app.MapGroup("/api/streamer")
            .RequireAuthorization(HubPolicies.Streamer)
            .AddEndpointFilter<StreamerTenantStatusFilter>();
        streamers.MapGet("/me", async (ClaimsPrincipal principal, HubDbContext db, CancellationToken cancellationToken) =>
        {
            if (!Guid.TryParse(principal.FindFirstValue("tenant_id"), out var tenantId))
                return HubErrorResults.Unauthorized("invalid_streamer_token", "The streamer token is invalid.");
            var tenant = await db.Streamers.AsNoTracking().SingleOrDefaultAsync(item => item.Id == tenantId, cancellationToken);
            return tenant is null
                ? HubErrorResults.Forbidden("tenant_unavailable", "This tenant is unavailable.")
                : Results.Ok(new StreamerProfileResponse(tenant.Id, tenant.Email, tenant.DisplayName, tenant.Slug, tenant.Status.ToStorageValue()));
        });
        streamers.MapPost("/sites", async (
            CreateSiteRequest request,
            ClaimsPrincipal principal,
            SiteCredentialService service,
            IHostEnvironment environment,
            CancellationToken cancellationToken) =>
        {
            var tenantId = GetTenantId(principal);
            var site = await service.RegisterSiteAsync(tenantId, request.Name, request.BaseUrl, environment.IsDevelopment(), cancellationToken);
            return Results.Created($"/api/streamer/sites/{site.Id:D}", site);
        }).RequireRateLimiting("streamer-sensitive");
        streamers.MapPost("/sites/{siteId:guid}/credentials/rotate", async (
            Guid siteId,
            ClaimsPrincipal principal,
            SiteCredentialService service,
            CancellationToken cancellationToken) =>
        {
            var credential = await service.RotateAsync(GetTenantId(principal), siteId, cancellationToken);
            return Results.Ok(credential);
        }).RequireRateLimiting("streamer-sensitive");
        streamers.MapDelete("/sites/{siteId:guid}/credentials/{credentialId:guid}", async (
            Guid siteId,
            Guid credentialId,
            ClaimsPrincipal principal,
            SiteCredentialService service,
            CancellationToken cancellationToken) =>
        {
            var found = await service.RevokeAsync(GetTenantId(principal), siteId, credentialId, cancellationToken);
            return found ? Results.NoContent() : Results.NotFound(new HubApiError("credential_not_found", "Credential was not found."));
        }).RequireRateLimiting("streamer-sensitive");

        var sites = app.MapGroup("/api/site")
            .RequireAuthorization(HubPolicies.Site)
            .AddEndpointFilter<SiteTenantStatusFilter>();
        sites.MapGet("/ping", (ClaimsPrincipal principal) => Results.Ok(new
        {
            status = "ok",
            tenantId = principal.FindFirstValue("tenant_id"),
            siteId = principal.FindFirstValue("site_id")
        }));

        app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false
        });
        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });
    }

    private static async Task<PlatformAdmin?> FindActiveAdminAsync(ClaimsPrincipal principal, HubDbContext db, CancellationToken cancellationToken)
    {
        var email = principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email)) return null;
        var normalized = email.Trim().ToLowerInvariant();
        return await db.PlatformAdmins.SingleOrDefaultAsync(item => item.EmailNormalized == normalized && item.DisabledAt == null, cancellationToken);
    }

    private static Guid GetTenantId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue("tenant_id"), out var tenantId)
            ? tenantId
            : throw new HubServiceException(StatusCodes.Status401Unauthorized, "invalid_streamer_token", "The streamer token is invalid.");

    private static (string? ClientId, string? ClientSecret) ReadClientCredentials(HttpRequest request, IFormCollection? form)
    {
        var authorization = request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorization[6..].Trim()));
                var separator = decoded.IndexOf(':');
                if (separator >= 0) return (decoded[..separator], decoded[(separator + 1)..]);
            }
            catch (FormatException)
            {
                return (null, null);
            }
        }
        return (form?["client_id"].ToString(), form?["client_secret"].ToString());
    }

    private static IResult OAuthTokenError(int statusCode, string code, string description) =>
        Results.Json(new OAuthTokenErrorResponse(code, description), statusCode: statusCode);
}

public sealed record RegistrationOptionsRequest(string InviteToken, string DisplayName, string Slug);
public sealed record StreamerLoginOptionsRequest(string Email);
public sealed record PasskeyVerifyRequest(Guid FlowId, System.Text.Json.JsonElement Credential);
public sealed record StreamerTokenResponse(Guid? TenantId, string AccessToken, DateTimeOffset ExpiresAt);
public sealed record OAuthTokenErrorResponse(
    [property: System.Text.Json.Serialization.JsonPropertyName("error")] string Error,
    [property: System.Text.Json.Serialization.JsonPropertyName("error_description")] string ErrorDescription);
public sealed record CreateInviteRequest(string Email, int? ExpiresInHours = null);
public sealed record CreatedInviteResponse(Guid Id, string Email, string InviteToken, DateTimeOffset ExpiresAt);
public sealed record SetTenantStatusRequest(string Status);
public sealed record TenantStatusResponse(Guid TenantId, string Status);
public sealed record CreateSiteRequest(string Name, string BaseUrl);
public sealed record StreamerProfileResponse(Guid Id, string Email, string DisplayName, string Slug, string Status);
