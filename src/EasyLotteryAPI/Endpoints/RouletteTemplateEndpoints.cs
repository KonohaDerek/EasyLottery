using EasyLotteryApi;
using EasyLotteryApplication.Templates;
using EasyLotteryDomain.Models.Entities;
using MediatR;

namespace EasyLotteryApi.Endpoints;

internal static class RouletteTemplateEndpoints
{
    public static void MapRouletteTemplateEndpoints(this WebApplication app)
    {
        app.MapGet("/api/roulette-templates", async (HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            return Results.Ok(await mediator.Send(new GetRouletteTemplatesQuery(), context.RequestAborted));
        });

        app.MapGet("/api/roulette-templates/{id:int}", async (int id, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var template = await mediator.Send(new GetRouletteTemplateQuery(id), context.RequestAborted);
            return template is null ? Results.NotFound() : Results.Ok(template);
        });

        app.MapGet("/api/roulette-templates/public/{publicId:guid}", async (Guid publicId, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var template = await mediator.Send(new GetRouletteTemplateByPublicIdQuery(publicId), context.RequestAborted);
            if (template is null) return Results.NotFound();
            var decision = access.RequireAdmin(context.Request);
            if (decision != ApiAccessDecision.Allowed)
            {
                var denied = ObsSessionAccess.DeniedResult(access.RequireObs(context.Request, "roulette", publicId.ToString(), "read"));
                if (denied is not null) return denied;
            }
            return Results.Ok(template);
        });

        app.MapPost("/api/roulette-templates", async (RouletteTemplate template, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var saved = await mediator.Send(new SaveRouletteTemplateCommand(template), context.RequestAborted);
            return Results.Created($"/api/roulette-templates/{saved.Id}", saved);
        }).RequireRateLimiting("sensitive");

        app.MapPut("/api/roulette-templates/{id:int}", async (int id, RouletteTemplate template, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            if (template.Id != 0 && template.Id != id)
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "請求資料無效", detail: "路由 ID 與模板 ID 不一致。");
            template.Id = id;
            return Results.Ok(await mediator.Send(new SaveRouletteTemplateCommand(template), context.RequestAborted));
        }).RequireRateLimiting("sensitive");

        app.MapDelete("/api/roulette-templates/{id:int}", async (int id, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            await mediator.Send(new DeleteRouletteTemplateCommand(id), context.RequestAborted);
            return Results.NoContent();
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/roulette-templates/{id:int}/duplicate", async (int id, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var duplicate = await mediator.Send(new DuplicateRouletteTemplateCommand(id), context.RequestAborted);
            return Results.Created($"/api/roulette-templates/{duplicate.Id}", duplicate);
        }).RequireRateLimiting("sensitive");

        app.MapPut("/api/roulette-templates/{id:int}/publication-status", async (int id, PublicationStatusRequest request, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            return Results.Ok(await mediator.Send(new SetRoulettePublicationStatusCommand(id, request.Status), context.RequestAborted));
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/roulette-templates/{id:int}/spin", async (int id, SpinRequest request, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            return Results.Ok(await mediator.Send(new SpinRouletteCommand(id, request.ForceIndex, request.CurrentRotation), context.RequestAborted));
        }).RequireRateLimiting("sensitive");

        app.MapGet("/api/roulette-templates/{id:int}/export", async (int id, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            return Results.Text(await mediator.Send(new ExportRouletteTemplateQuery(id), context.RequestAborted), "application/json");
        });

        app.MapPost("/api/roulette-templates/import", async (HttpRequest request, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            using var reader = new StreamReader(request.Body);
            var json = await reader.ReadToEndAsync(context.RequestAborted);
            return Results.Created("/api/roulette-templates", await mediator.Send(new ImportRouletteTemplateCommand(json), context.RequestAborted));
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/roulette-templates/seed-defaults", async (HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            await mediator.Send(new SeedRouletteTemplatesCommand(), context.RequestAborted);
            return Results.NoContent();
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/roulette-templates/public/{publicId:guid}/spin", async (Guid publicId, SpinRequest request, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var template = await mediator.Send(new GetRouletteTemplateByPublicIdQuery(publicId), context.RequestAborted);
            if (template is null) return Results.NotFound();
            var denied = ObsSessionAccess.DeniedResult(access.RequireObs(context.Request, "roulette", publicId.ToString(), "control"));
            if (denied is not null) return denied;
            return Results.Ok(await mediator.Send(new SpinRouletteCommand(template.Id, request.ForceIndex, request.CurrentRotation), context.RequestAborted));
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/roulette-templates/public/{publicId:guid}/record-result", async (Guid publicId, SpinResult result, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var template = await mediator.Send(new GetRouletteTemplateByPublicIdQuery(publicId), context.RequestAborted);
            if (template is null) return Results.NotFound();
            var denied = ObsSessionAccess.DeniedResult(access.RequireObs(context.Request, "roulette", publicId.ToString(), "control"));
            if (denied is not null) return denied;
            return Results.Ok(await mediator.Send(new RecordRouletteResultCommand(template.Id, result), context.RequestAborted));
        }).RequireRateLimiting("sensitive");
    }
}

internal sealed record SpinRequest(int? ForceIndex, double CurrentRotation);
