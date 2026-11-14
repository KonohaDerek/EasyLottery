using System.Security.Cryptography;
using EasyLotteryApi.Security;

namespace EasyLotteryApi.CardShop;

internal static class CardHubLinkEndpoints
{
    public static void MapCardHubLinkEndpoints(this WebApplication app)
    {
        app.MapGet("/api/settings/card-hub", async (HttpContext context, ICardHubLinkRepository repository, ObsSessionAccess access, CancellationToken cancellationToken) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var link = await repository.GetAsync(cancellationToken);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(ToResponse(link));
        });

        app.MapPut("/api/settings/card-hub", async (
            CardHubLinkUpdate request,
            HttpContext context,
            ICardHubLinkRepository repository,
            ICardHubSecretProtector protector,
            ICardHubConnectionVerifier verifier,
            CardHubOriginPolicy originPolicy,
            ObsSessionAccess access,
            CancellationToken cancellationToken) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            if (!originPolicy.TryGetAllowedOrigin(request.HubUrl, out var hubOrigin) || string.IsNullOrWhiteSpace(request.SiteClientId))
                return Results.BadRequest(new { code = "invalid_card_hub_settings", error = "請輸入有效的 Card Hub 網址與 Client ID。" });

            var existing = await repository.GetAsync(cancellationToken);
            var secret = request.ClientSecret;
            if (string.IsNullOrWhiteSpace(secret) && existing is not null)
            {
                try
                {
                    secret = protector.Unprotect(existing.EncryptedSecret);
                }
                catch (CryptographicException)
                {
                    return Results.Conflict(new { code = "card_hub_secret_unavailable", error = "已保存的密鑰無法解開，請重新輸入 Client Secret。" });
                }
            }

            if (string.IsNullOrWhiteSpace(secret))
                return Results.BadRequest(new { code = "card_hub_secret_required", error = "請輸入 Card Hub Client Secret。" });

            var checkedAt = DateTimeOffset.UtcNow;
            var state = await verifier.VerifyAsync(request.HubUrl, request.SiteClientId, secret, cancellationToken);
            var record = new CardHubLinkRecord(
                hubOrigin.GetLeftPart(UriPartial.Authority),
                request.SiteClientId.Trim(),
                protector.Protect(secret),
                existing?.TenantId,
                existing?.TenantName,
                ToPersistedState(state),
                checkedAt,
                existing?.LastPublishAtUtc);
            await repository.SaveAsync(record, cancellationToken);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(ToResponse(record));
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/settings/card-hub/test", async (
            HttpContext context,
            ICardHubLinkRepository repository,
            ICardHubSecretProtector protector,
            ICardHubConnectionVerifier verifier,
            CardHubOriginPolicy originPolicy,
            ObsSessionAccess access,
            CancellationToken cancellationToken) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var link = await repository.GetAsync(cancellationToken);
            if (link is null) return Results.Conflict(new { code = "card_hub_not_linked", error = "Card Hub 尚未連結。" });
            if (!originPolicy.IsAllowed(link.HubUrl))
                return Results.BadRequest(new { code = "card_hub_origin_not_trusted", error = "Card Hub 網址不在允許的來源清單中。" });

            string secret;
            try
            {
                secret = protector.Unprotect(link.EncryptedSecret);
            }
            catch (CryptographicException)
            {
                return Results.Conflict(new { code = "card_hub_secret_unavailable", error = "已保存的密鑰無法解開，請重新輸入 Client Secret。" });
            }

            var state = await verifier.VerifyAsync(link.HubUrl, link.SiteClientId, secret, cancellationToken);
            var updated = link with { ConnectionState = ToPersistedState(state), LastCheckedAtUtc = DateTimeOffset.UtcNow };
            await repository.SaveAsync(updated, cancellationToken);
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(ToResponse(updated));
        }).RequireRateLimiting("sensitive");

        app.MapDelete("/api/settings/card-hub", async (HttpContext context, ICardHubLinkRepository repository, ObsSessionAccess access, CancellationToken cancellationToken) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            await repository.DeleteAsync(cancellationToken);
            return Results.NoContent();
        }).RequireRateLimiting("sensitive");
    }

    private static CardHubLinkResponse ToResponse(CardHubLinkRecord? link) => link is null
        ? new("not_linked", "", "", false, null, null, null, null)
        : new(link.ConnectionState, link.HubUrl, link.SiteClientId, true, link.TenantId, link.TenantName, link.LastCheckedAtUtc, link.LastPublishAtUtc);

    private static string ToPersistedState(CardHubConnectionState state) => state switch
    {
        CardHubConnectionState.Linked => "linked",
        CardHubConnectionState.CredentialsInvalid => "credentials_invalid",
        _ => "unreachable"
    };

}

internal sealed record CardHubLinkUpdate(string HubUrl, string SiteClientId, string ClientSecret);

internal sealed record CardHubLinkResponse(
    string Status,
    string HubUrl,
    string SiteClientId,
    bool HasSecret,
    string? TenantId,
    string? TenantName,
    DateTimeOffset? LastCheckedAtUtc,
    DateTimeOffset? LastPublishAtUtc);
