using System.Text.Json;
using EasyLotteryApi.Passkey;
using EasyLotteryApi.Security;
using EasyLotteryApi.Security.Endpoints;
using Microsoft.AspNetCore.WebUtilities;

namespace EasyLotteryApi.Passkey.Endpoints;

internal static class PasskeyEndpoints
{
    public static void MapPasskeyEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/passkey/options", async (
            PasskeyOptionsRequest request,
            HttpContext context,
            PasskeyAuthenticationService authentication,
            CancellationToken cancellationToken) =>
        {
            try
            {
                return Results.Ok(await authentication.BeginAsync(context, request, cancellationToken));
            }
            catch (PasskeyAuthenticationException exception)
            {
                return Results.Json(new { error = exception.Message, code = exception.Code }, statusCode: exception.StatusCode);
            }
        }).RequireRateLimiting("authentication");

        app.MapPost("/api/auth/passkey/verify", async (
            PasskeyVerifyRequest request,
            PasskeyAuthenticationService authentication,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var issued = await authentication.FinishAsync(request, cancellationToken);
                return Results.Ok(new SessionTokenResponse(issued.Token, issued.ExpiresAtUtc));
            }
            catch (PasskeyAuthenticationException exception)
            {
                return Results.Json(new { error = exception.Message, code = exception.Code }, statusCode: exception.StatusCode);
            }
        }).RequireRateLimiting("authentication");

        app.MapGet("/api/admin/passkeys", async (
            HttpContext context,
            ObsSessionAccess access,
            PasskeyStateStore store,
            CancellationToken cancellationToken) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;

            var state = await store.ReadAsync(cancellationToken);
            return Results.Ok(state.Credentials.Select(PasskeyAuthenticationService.ToDeviceResponse));
        });

        app.MapPost("/api/admin/passkeys/options", async (
            PasskeyManageOptionsRequest request,
            HttpContext context,
            ObsSessionAccess access,
            PasskeyAuthenticationService authentication,
            CancellationToken cancellationToken) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;

            try
            {
                return Results.Ok(await authentication.BeginAddAsync(request.Name, cancellationToken));
            }
            catch (PasskeyAuthenticationException exception)
            {
                return Results.Json(new { error = exception.Message, code = exception.Code }, statusCode: exception.StatusCode);
            }
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/admin/passkeys/verify", async (
            PasskeyManageVerifyRequest request,
            HttpContext context,
            ObsSessionAccess access,
            PasskeyAuthenticationService authentication,
            CancellationToken cancellationToken) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;

            try
            {
                return Results.Ok(await authentication.FinishAddAsync(request, cancellationToken));
            }
            catch (PasskeyAuthenticationException exception)
            {
                return Results.Json(new { error = exception.Message, code = exception.Code }, statusCode: exception.StatusCode);
            }
        }).RequireRateLimiting("sensitive");

        app.MapDelete("/api/admin/passkeys/{id}", async (
            string id,
            HttpContext context,
            ObsSessionAccess access,
            PasskeyStateStore store,
            CancellationToken cancellationToken) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;

            byte[] credentialId;
            try
            {
                credentialId = WebEncoders.Base64UrlDecode(id);
            }
            catch (FormatException)
            {
                return Results.BadRequest(new { error = "Passkey ID 無效。", code = "credential_invalid" });
            }

            return await store.RemoveCredentialAsync(credentialId, cancellationToken) switch
            {
                PasskeyRemoveResult.Removed => Results.NoContent(),
                PasskeyRemoveResult.LastCredential => Results.Conflict(new { error = "至少要保留一個 Passkey。", code = "last_passkey" }),
                _ => Results.NotFound(new { error = "找不到 Passkey。", code = "passkey_not_found" })
            };
        }).RequireRateLimiting("sensitive");
    }
}
