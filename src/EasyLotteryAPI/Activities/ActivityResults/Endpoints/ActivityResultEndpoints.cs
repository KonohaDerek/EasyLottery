using EasyLotteryApplication.Templates;
using EasyLotteryDomain.Models.Entities;
using EasyLotteryApi.Security;
using MediatR;
using Results = Microsoft.AspNetCore.Http.Results;

namespace EasyLotteryApi.Activities.ActivityResults.Endpoints;

internal static class ActivityResultEndpoints
{
    public static void MapActivityResultEndpoints(this WebApplication app)
    {
        app.MapGet("/api/activity-results", async (HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            return Results.Ok(await mediator.Send(new GetActivityResultsQuery(), context.RequestAborted));
        });

        app.MapGet("/api/activity-results/{id:int}", async (int id, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var result = await mediator.Send(new GetActivityResultQuery(id), context.RequestAborted);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        app.MapPost("/api/activity-results/poke", async (RecordPokeResultRequest request, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            return Results.Ok(await mediator.Send(new RecordPokeResultCommand(request.TemplateId), context.RequestAborted));
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/activity-results/roulette", async (RecordRouletteResultRequest request, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            return Results.Ok(await mediator.Send(new RecordRouletteResultCommand(request.TemplateId, request.SpinResult), context.RequestAborted));
        }).RequireRateLimiting("sensitive");
    }
}

internal sealed record RecordPokeResultRequest(int TemplateId);
internal sealed record RecordRouletteResultRequest(int TemplateId, SpinResult SpinResult);
