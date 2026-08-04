using EasyLotteryDomain.Models;

namespace EasyLotteryApi.Endpoints;

internal static class LiveDrawSessionEndpoints
{
    public static void MapLiveDrawSessionEndpoints(this WebApplication app)
    {
        app.MapGet("/api/live-draw/{kind}/{publicId:guid}", async (
            string kind, Guid publicId, HttpContext context, LiveDrawSessionService sessions, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireObs(context.Request, kind, publicId.ToString(), "read"));
            if (denied is not null) return denied;
            if (!TryParseKind(kind, out var parsedKind)) return Results.BadRequest(new { error = "不支援的抽獎類型。" });
            var state = await sessions.GetAsync(parsedKind, publicId);
            return state is null ? Results.NotFound(new { error = "找不到即時抽獎工作階段。" }) : Results.Ok(state);
        });

        app.MapPost("/api/live-draw/pokebox/{publicId:guid}/poke", async (
            Guid publicId, PokeLiveDrawCommand command, HttpContext context, LiveDrawSessionService sessions, ObsSessionAccess access) =>
            await ExecuteAsync(context, access, "pokebox", publicId, () => sessions.PokeAsync(publicId, command, CancellationToken.None)))
            .RequireRateLimiting("sensitive");

        app.MapPost("/api/live-draw/pokebox/{publicId:guid}/reset", async (
            Guid publicId, HttpContext context, LiveDrawSessionService sessions, ObsSessionAccess access) =>
            await ExecuteAsync(context, access, "pokebox", publicId, () => sessions.ResetPokeAsync(publicId, CancellationToken.None)))
            .RequireRateLimiting("sensitive");

        app.MapPost("/api/live-draw/roulette/{publicId:guid}/spin", async (
            Guid publicId, RouletteLiveDrawCommand command, HttpContext context, LiveDrawSessionService sessions, ObsSessionAccess access) =>
            await ExecuteAsync(context, access, "roulette", publicId, () => sessions.SpinAsync(publicId, command, CancellationToken.None)))
            .RequireRateLimiting("sensitive");
    }

    private static async Task<IResult> ExecuteAsync(
        HttpContext context,
        ObsSessionAccess access,
        string kind,
        Guid publicId,
        Func<Task<LiveDrawSessionState>> action)
    {
        var denied = ObsSessionAccess.DeniedResult(access.RequireObs(context.Request, kind, publicId.ToString(), "control"));
        if (denied is not null) return denied;
        try
        {
            return Results.Ok(await action());
        }
        catch (KeyNotFoundException exception)
        {
            return Results.NotFound(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
    }

    private static bool TryParseKind(string value, out LiveDrawSessionKind kind)
    {
        if (value.Equals("pokebox", StringComparison.OrdinalIgnoreCase))
        {
            kind = LiveDrawSessionKind.PokeBox;
            return true;
        }

        if (value.Equals("roulette", StringComparison.OrdinalIgnoreCase))
        {
            kind = LiveDrawSessionKind.Roulette;
            return true;
        }

        kind = default;
        return false;
    }
}
