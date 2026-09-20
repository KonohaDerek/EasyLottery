using EasyLotteryApi.Security;
using EasyLotteryInfrastructure.Settings;

namespace EasyLotteryApi.Interactions;

internal static class TwitchOAuthEndpoints
{
    public static void MapPlatformConnectionEndpoints(this WebApplication app)
    {
        app.MapGet("/api/interactions/connections", async (HttpContext context, PlatformConnectionService connections, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var snapshot = await connections.ListAsync(context.RequestAborted);
            context.Response.Headers.ETag = snapshot.ETag;
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(snapshot.Connections);
        });

        app.MapPut("/api/interactions/connections/{platform}", async (string platform, PlatformConnectionUpdate update, HttpContext context, PlatformConnectionService connections, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            try
            {
                var snapshot = await connections.UpdateAsync(platform, update, context.Request.Headers.IfMatch.FirstOrDefault(), context.RequestAborted);
                context.Response.Headers.ETag = snapshot.ETag;
                context.Response.Headers.CacheControl = "no-store";
                return Results.Ok(snapshot.Connections.Single(item => item.Platform.Equals(platform, StringComparison.OrdinalIgnoreCase)));
            }
            catch (ConfigurationConcurrencyException exception) { return Results.Conflict(new { error = exception.Message, expectedETag = exception.ExpectedETag, actualETag = exception.ActualETag }); }
            catch (InvalidOperationException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/interactions/connections/twitch/oauth", (HttpContext context, PlatformConnectionService connections, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            try { return Results.Ok(connections.BeginTwitchOAuth()); }
            catch (InvalidOperationException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireRateLimiting("sensitive");

        app.MapGet("/api/interactions/connections/twitch/callback", async (string? state, string? code, PlatformConnectionService connections, HttpContext context) =>
            await connections.TryCompleteTwitchOAuthAsync(state ?? "", code ?? "", context.RequestAborted)
                ? Results.NoContent()
                : Results.BadRequest(new { error = "OAuth state 無效或已過期。" }));
    }
}
