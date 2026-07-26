using EasyLotteryApi;

namespace EasyLotteryApi.Endpoints;

internal static class TunnelEndpoints
{
    public static void MapTunnelEndpoints(this WebApplication app)
    {
        app.MapGet("/api/tunnel", (TunnelRuntimeService tunnelRuntime) => Results.Ok(tunnelRuntime.GetStatus()));
        app.MapPost("/api/tunnel/start", async (TunnelStartRequest request, TunnelRuntimeService tunnelRuntime, CancellationToken cancellationToken) =>
            Results.Ok(await tunnelRuntime.StartAsync(request.Provider, cancellationToken)));
        app.MapDelete("/api/tunnel", async (TunnelRuntimeService tunnelRuntime, CancellationToken cancellationToken) =>
        {
            await tunnelRuntime.StopAsync(cancellationToken);
            return Results.NoContent();
        });
    }
}
