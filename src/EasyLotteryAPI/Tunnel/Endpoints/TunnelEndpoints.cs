using EasyLotteryApi.Security;
using EasyLotteryApi.Tunnel;

namespace EasyLotteryApi.Tunnel.Endpoints;

internal static class TunnelEndpoints
{
    public static void MapTunnelEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tunnel", (HttpContext context, TunnelRuntimeService tunnelRuntime, ObsSessionAccess access) =>
            ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request)) ?? Results.Ok(tunnelRuntime.GetStatus()));
        app.MapPost("/api/tunnel/start", async (TunnelStartRequest request, HttpContext context, TunnelRuntimeService tunnelRuntime, ObsSessionAccess access, CancellationToken cancellationToken) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            if (!TunnelProviders.TryParse(request.Provider, out var provider))
                return Results.BadRequest(new { error = "不支援的 tunnel provider。" });
            return Results.Ok(await tunnelRuntime.StartAsync(provider, cancellationToken));
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
