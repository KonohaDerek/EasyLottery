using System.Security.Cryptography;
using System.Text;
using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Domain;
using EasyLotteryCardHub.Invites;

namespace EasyLotteryCardHub.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/hub/v1/admin").RequireRateLimiting("hub-admin").AddEndpointFilter(async (context, next) =>
        {
            var environment = context.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
            var remoteIp = context.HttpContext.Connection.RemoteIpAddress;
            var localDevelopmentRequest = environment.IsDevelopment() && (remoteIp is null || System.Net.IPAddress.IsLoopback(remoteIp));
            if (!context.HttpContext.Request.IsHttps && !localDevelopmentRequest)
                return Results.BadRequest(new { error = "https_required" });

            var configuredKey = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>()["CardHub:Admin:ApiKey"];
            var suppliedKey = context.HttpContext.Request.Headers["X-CardHub-Admin-Key"].ToString();
            if (string.IsNullOrWhiteSpace(configuredKey) || !SecureEquals(suppliedKey, configuredKey))
                return Results.Unauthorized();
            return await next(context);
        });

        group.MapPost("/streamer-invites", async (CreateStreamerInviteRequest request, CardHubInviteService invitations, CancellationToken cancellationToken) =>
        {
            try
            {
                var invite = await invitations.CreateAsync(request, "platform-admin", cancellationToken);
                return Results.Accepted($"/hub/v1/admin/streamer-invites/{invite.Id:D}", invite);
            }
            catch (ArgumentException)
            {
                return Results.BadRequest(new { error = "invite_request_invalid" });
            }
            catch (InvalidOperationException exception) when (exception.Message.StartsWith("CardHub:EmailOutbox:", StringComparison.Ordinal))
            {
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "邀請信 outbox 尚未設定加密金鑰");
            }
            catch (InvalidOperationException exception) when (exception.Message.StartsWith("CardHub:Identity:", StringComparison.Ordinal))
            {
                return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Card Hub identity hashing is not configured");
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict(new { error = "streamer_already_registered_or_invited" });
            }
        });

        group.MapPut("/streamers/{tenantId:guid}/status", async (Guid tenantId, UpdateTenantStatusRequest request, PostgresCardHubStore store, CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<TenantStatus>(request.Status, true, out var status))
                return Results.BadRequest(new { error = "status_invalid" });
            return await store.SetTenantStatusAsync(tenantId, status, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound();
        });

        group.MapPost("/sites/{siteId:guid}/credentials/rotate", async (Guid siteId, PostgresCardHubStore store, CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await store.RotateCredentialAsync(siteId, cancellationToken));
            }
            catch (KeyNotFoundException)
            {
                return Results.NotFound(new { error = "site_not_found" });
            }
        });

        group.MapPost("/sites/{siteId:guid}/credentials/{credentialId:guid}/revoke", async (Guid siteId, Guid credentialId, PostgresCardHubStore store, CancellationToken cancellationToken) =>
            await store.RevokeCredentialAsync(siteId, credentialId, cancellationToken)
                ? Results.NoContent()
                : Results.NotFound(new { error = "credential_not_found" }));

        return group;
    }

    private static bool SecureEquals(string supplied, string expected)
    {
        var left = Encoding.UTF8.GetBytes(supplied);
        var right = Encoding.UTF8.GetBytes(expected);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    public static bool IsValidSlug(string? slug) => !string.IsNullOrWhiteSpace(slug)
        && slug.Length <= 80
        && slug.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-');

    public sealed record UpdateTenantStatusRequest(string Status);
}
