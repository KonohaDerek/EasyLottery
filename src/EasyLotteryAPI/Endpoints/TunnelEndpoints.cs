using EasyLotteryApi;

namespace EasyLotteryApi.Endpoints;

internal static class TunnelEndpoints
{
    public static void MapTunnelEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tunnel", (HttpContext context, TunnelRuntimeService tunnelRuntime, ObsSessionAccess access) =>
            ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request)) ?? Results.Ok(tunnelRuntime.GetStatus()));
        app.MapPost("/api/tunnel/start", async (TunnelStartRequest request, HttpContext context, TunnelRuntimeService tunnelRuntime, ObsSessionAccess access, CancellationToken cancellationToken) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            return denied ?? Results.Ok(await tunnelRuntime.StartAsync(request.Provider, cancellationToken));
        }).RequireRateLimiting("sensitive");
        app.MapDelete("/api/tunnel", async (HttpContext context, TunnelRuntimeService tunnelRuntime, ObsSessionAccess access, CancellationToken cancellationToken) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            await tunnelRuntime.StopAsync(cancellationToken);
            return Results.NoContent();
        }).RequireRateLimiting("sensitive");
    }
}
