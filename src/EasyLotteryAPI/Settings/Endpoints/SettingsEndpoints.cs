using System.Text;
using EasyLotteryApi.Common;
using EasyLotteryApi.Obs;
using EasyLotteryApi.Security;
using EasyLotteryApi.Settings;
using EasyLotteryApplication.Settings;
using EasyLotteryDomain.Services;
using Microsoft.AspNetCore.SignalR;

namespace EasyLotteryApi.Settings.Endpoints;

internal static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this WebApplication app)
    {
        Func<HttpContext, IEasyLotteryConfigRepository, ObsSettingsProjectionService, ObsSessionAccess, Task<IResult>> readSettings = async (context, settingsStore, projection, sessionAccess) =>
        {
            MarkCompatibilityEndpoint(context);
            var adminDecision = sessionAccess.RequireAdmin(context.Request);
            if (adminDecision == ApiAccessDecision.Allowed)
            {
                var snapshot = await settingsStore.ReadForBrowserSnapshotAsync(context.RequestAborted);
                context.Response.Headers.ETag = snapshot.ETag;
                return Results.Text(snapshot.Yaml, "text/yaml", Encoding.UTF8);
            }

            var principal = sessionAccess.ReadPrincipal(context.Request);
            if (principal?.FindFirst("token_use")?.Value != ObsSessionTokenService.ObsUse) return Results.Unauthorized();
            if (!HasScope(principal, ObsSessionScope.Read)) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (!ObsResourceKinds.TryParse(principal.FindFirst("resource_kind")?.Value, out var kind))
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            var resourceId = principal.FindFirst("resource_id")?.Value ?? "";
            var yaml = await projection.ReadAsync(kind, resourceId, context.RequestAborted);
            return yaml is null ? Results.NotFound() : Results.Text(yaml, "text/yaml", Encoding.UTF8);
        };
        app.MapGet("/settings", readSettings);
        app.MapGet("/easy-lottery-config.yaml", readSettings);

        Func<HttpContext, IHubContext<OvertimeHub>, IEasyLotteryConfigRepository, ObsSettingsProjectionService, ObsSessionAccess, Task<IResult>> writeSettings = async (context, hub, settingsStore, projection, sessionAccess) =>
        {
            MarkCompatibilityEndpoint(context);
            var adminDecision = sessionAccess.RequireAdmin(context.Request);
            var principal = adminDecision == ApiAccessDecision.Allowed ? null : sessionAccess.ReadPrincipal(context.Request);
            var isObsControl = principal?.FindFirst("token_use")?.Value == ObsSessionTokenService.ObsUse
                && HasScope(principal, ObsSessionScope.Control);
            if (adminDecision != ApiAccessDecision.Allowed && !isObsControl)
                return adminDecision == ApiAccessDecision.Unauthorized ? Results.Unauthorized() : Results.StatusCode(StatusCodes.Status403Forbidden);

            var content = await HttpRequestBodyReader.ReadTextAsync(context.Request, context.RequestAborted);
            if (adminDecision == ApiAccessDecision.Allowed)
            {
                var expectedETag = context.Request.Headers.IfMatch.FirstOrDefault();
                await settingsStore.SaveBrowserUpdateAsync(content, expectedETag, context.RequestAborted);
                var snapshot = await settingsStore.ReadForBrowserSnapshotAsync(context.RequestAborted);
                context.Response.Headers.ETag = snapshot.ETag;
            }
            else
            {
                if (!ObsResourceKinds.TryParse(principal!.FindFirst("resource_kind")?.Value, out var kind)
                    || !ObsResourceKinds.SettingsWritable.Contains(kind))
                    return Results.StatusCode(StatusCodes.Status403Forbidden);
                var resourceId = principal.FindFirst("resource_id")?.Value ?? "";
                await projection.UpdateResourceAsync(kind, resourceId, content, context.RequestAborted);
            }
            await hub.Clients.Group(OvertimeHub.GroupName).SendAsync("OvertimeStateChanged", context.RequestAborted);
            return Results.NoContent();
        };
        app.MapPut("/settings", writeSettings).RequireRateLimiting("sensitive");
        app.MapPut("/easy-lottery-config.yaml", writeSettings).RequireRateLimiting("sensitive");

        app.MapGet("/api/settings/backups", (HttpContext context, IEasyLotteryConfigRepository settingsStore, ObsSessionAccess sessionAccess) =>
        {
            var decision = sessionAccess.RequireAdmin(context.Request);
            return ObsSessionAccess.DeniedResult(decision) ?? Results.Ok(settingsStore.ListBackups());
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/settings/backups/{id}/restore", async (string id, HttpContext context, IEasyLotteryConfigRepository settingsStore, ObsSessionAccess sessionAccess) =>
        {
            var decision = sessionAccess.RequireAdmin(context.Request);
            if (decision != ApiAccessDecision.Allowed)
                return decision == ApiAccessDecision.Unauthorized ? Results.Unauthorized() : Results.StatusCode(StatusCodes.Status403Forbidden);

            await settingsStore.RestoreBackupAsync(id, context.RequestAborted);
            return Results.NoContent();
        }).RequireRateLimiting("sensitive");
    }

    private static void MarkCompatibilityEndpoint(HttpContext context)
    {
        context.Response.Headers["Deprecation"] = "true";
        context.Response.Headers["Sunset"] = "Wed, 31 Dec 2026 00:00:00 GMT";
        context.Response.Headers["Link"] = "</api/settings/obs-layout>; rel=successor-version";
    }

    private static bool HasScope(System.Security.Claims.ClaimsPrincipal principal, ObsSessionScope scope) =>
        principal.FindAll("scope").Any(claim => claim.Value == scope.ToValue());
}
