using EasyLotteryDomain.Models;
using EasyLotteryApi.Obs;
using EasyLotteryApi.Security;

namespace EasyLotteryApi.Obs.Endpoints;

internal static class LiveDrawSessionEndpoints
{
    public static void MapLiveDrawSessionEndpoints(this WebApplication app)
    {
        app.MapGet("/api/live-draw/{kind}/{publicId:guid}", async (
            string kind, Guid publicId, HttpContext context, LiveDrawSessionService sessions, ObsSessionAccess access) =>
        {
            if (!TryParseKind(kind, out var parsedKind, out var resourceKind)) return Results.BadRequest(new { error = "不支援的抽獎類型。" });
            var denied = ObsSessionAccess.DeniedResult(access.RequireObs(context.Request, resourceKind, publicId.ToString(), ObsSessionScope.Read));
            if (denied is not null) return denied;
            var state = await sessions.GetAsync(parsedKind, publicId);
            return state is null ? Results.NotFound(new { error = "找不到即時抽獎工作階段。" }) : Results.Ok(state);
        });

        app.MapPost("/api/live-draw/pokebox/{publicId:guid}/poke", async (
            Guid publicId, PokeLiveDrawCommand command, HttpContext context, LiveDrawSessionService sessions, ObsSessionAccess access) =>
            await ExecuteAsync(context, access, ObsResourceKind.PokeBox, publicId, () => sessions.PokeAsync(publicId, command, CancellationToken.None)))
            .RequireRateLimiting("sensitive");

        app.MapPost("/api/live-draw/pokebox/{publicId:guid}/reset", async (
            Guid publicId, HttpContext context, LiveDrawSessionService sessions, ObsSessionAccess access) =>
            await ExecuteAsync(context, access, ObsResourceKind.PokeBox, publicId, () => sessions.ResetPokeAsync(publicId, CancellationToken.None)))
            .RequireRateLimiting("sensitive");

        app.MapPost("/api/live-draw/roulette/{publicId:guid}/spin", async (
            Guid publicId, RouletteLiveDrawCommand command, HttpContext context, LiveDrawSessionService sessions, ObsSessionAccess access) =>
            await ExecuteAsync(context, access, ObsResourceKind.Roulette, publicId, () => sessions.SpinAsync(publicId, command, CancellationToken.None)))
            .RequireRateLimiting("sensitive");
    }

    private static async Task<IResult> ExecuteAsync(
        HttpContext context,
        ObsSessionAccess access,
        ObsResourceKind kind,
        Guid publicId,
        Func<Task<LiveDrawSessionState>> action)
    {
        var denied = ObsSessionAccess.DeniedResult(access.RequireObs(context.Request, kind, publicId.ToString(), ObsSessionScope.Control));
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

    private static bool TryParseKind(string value, out LiveDrawSessionKind kind, out ObsResourceKind resourceKind)
    {
        if (ObsResourceKinds.TryParse(value, out resourceKind) && LiveDrawSessionGroups.TryGetSessionKind(resourceKind, out kind))
            return true;
        kind = default;
        resourceKind = default;
        return false;
    }
}
