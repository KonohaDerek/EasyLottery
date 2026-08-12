using EasyLotteryApplication.Templates;
using EasyLotteryDomain.Models.Entities;
using EasyLotteryApi.Security;
using MediatR;
using EasyLotteryApi.Activities.Common;
using Results = Microsoft.AspNetCore.Http.Results;

namespace EasyLotteryApi.Activities.PokeBox.Endpoints;

internal static class PokeTemplateEndpoints
{
    public static void MapPokeTemplateEndpoints(this WebApplication app)
    {
        app.MapGet("/api/poke-templates", async (HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            return Results.Ok(await mediator.Send(new GetPokeTemplatesQuery(), context.RequestAborted));
        });

        app.MapGet("/api/poke-templates/{id:int}", async (int id, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var template = await mediator.Send(new GetPokeTemplateQuery(id), context.RequestAborted);
            return template is null ? Results.NotFound() : Results.Ok(template);
        });

        app.MapGet("/api/poke-templates/public/{publicId:guid}", async (Guid publicId, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var template = await mediator.Send(new GetPokeTemplateByPublicIdQuery(publicId), context.RequestAborted);
            if (template is null) return Results.NotFound();
            var decision = access.RequireAdmin(context.Request);
            if (decision != ApiAccessDecision.Allowed)
            {
                var denied = ObsSessionAccess.DeniedResult(access.RequireObs(context.Request, ObsResourceKind.PokeBox, publicId.ToString(), ObsSessionScope.Read));
                if (denied is not null) return denied;
            }
            return Results.Ok(template);
        });

        app.MapPost("/api/poke-templates", async (PokeTemplate template, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var saved = await mediator.Send(new SavePokeTemplateCommand(template), context.RequestAborted);
            return Results.Created($"/api/poke-templates/{saved.Id}", saved);
        }).RequireRateLimiting("sensitive");

        app.MapPut("/api/poke-templates/{id:int}", async (int id, PokeTemplate template, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            if (template.Id != 0 && template.Id != id)
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "請求資料無效", detail: "路由 ID 與模板 ID 不一致。");
            template.Id = id;
            return Results.Ok(await mediator.Send(new SavePokeTemplateCommand(template), context.RequestAborted));
        }).RequireRateLimiting("sensitive");

        app.MapDelete("/api/poke-templates/{id:int}", async (int id, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            await mediator.Send(new DeletePokeTemplateCommand(id), context.RequestAborted);
            return Results.NoContent();
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/poke-templates/{id:int}/duplicate", async (int id, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var duplicate = await mediator.Send(new DuplicatePokeTemplateCommand(id), context.RequestAborted);
            return Results.Created($"/api/poke-templates/{duplicate.Id}", duplicate);
        }).RequireRateLimiting("sensitive");

        app.MapPut("/api/poke-templates/{id:int}/publication-status", async (int id, PublicationStatusRequest request, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            return Results.Ok(await mediator.Send(new SetPokePublicationStatusCommand(id, request.Status), context.RequestAborted));
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/poke-templates/{id:int}/draw", async (int id, PokeDrawRequest request, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            return Results.Ok(await mediator.Send(new PokeCellCommand(id, request.Index), context.RequestAborted));
        }).RequireRateLimiting("sensitive");

        app.MapGet("/api/poke-templates/{id:int}/reveal-state", async (int id, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            return Results.Ok(await mediator.Send(new GetPokeRevealStateQuery(id), context.RequestAborted));
        });

        app.MapPost("/api/poke-templates/{id:int}/reset", async (int id, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            await mediator.Send(new ResetPokeTemplateCommand(id), context.RequestAborted);
            return Results.NoContent();
        }).RequireRateLimiting("sensitive");

        app.MapGet("/api/poke-templates/{id:int}/export", async (int id, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            return Results.Text(await mediator.Send(new ExportPokeTemplateQuery(id), context.RequestAborted), "application/json");
        });

        app.MapPost("/api/poke-templates/import", async (HttpRequest request, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            using var reader = new StreamReader(request.Body);
            var json = await reader.ReadToEndAsync(context.RequestAborted);
            return Results.Created("/api/poke-templates", await mediator.Send(new ImportPokeTemplateCommand(json), context.RequestAborted));
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/poke-templates/seed-defaults", async (HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            await mediator.Send(new SeedPokeTemplatesCommand(), context.RequestAborted);
            return Results.NoContent();
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/poke-templates/public/{publicId:guid}/draw", async (Guid publicId, PokeDrawRequest request, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var template = await mediator.Send(new GetPokeTemplateByPublicIdQuery(publicId), context.RequestAborted);
            if (template is null) return Results.NotFound();
            var denied = ObsSessionAccess.DeniedResult(access.RequireObs(context.Request, ObsResourceKind.PokeBox, publicId.ToString(), ObsSessionScope.Control));
            if (denied is not null) return denied;
            return Results.Ok(await mediator.Send(new PokeCellCommand(template.Id, request.Index), context.RequestAborted));
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/poke-templates/public/{publicId:guid}/reset", async (Guid publicId, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var template = await mediator.Send(new GetPokeTemplateByPublicIdQuery(publicId), context.RequestAborted);
            if (template is null) return Results.NotFound();
            var denied = ObsSessionAccess.DeniedResult(access.RequireObs(context.Request, ObsResourceKind.PokeBox, publicId.ToString(), ObsSessionScope.Control));
            if (denied is not null) return denied;
            await mediator.Send(new ResetPokeTemplateCommand(template.Id), context.RequestAborted);
            return Results.NoContent();
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/poke-templates/public/{publicId:guid}/record-result", async (Guid publicId, HttpContext context, IMediator mediator, ObsSessionAccess access) =>
        {
            var template = await mediator.Send(new GetPokeTemplateByPublicIdQuery(publicId), context.RequestAborted);
            if (template is null) return Results.NotFound();
            var denied = ObsSessionAccess.DeniedResult(access.RequireObs(context.Request, ObsResourceKind.PokeBox, publicId.ToString(), ObsSessionScope.Control));
            if (denied is not null) return denied;
            return Results.Ok(await mediator.Send(new RecordPokeResultCommand(template.Id), context.RequestAborted));
        }).RequireRateLimiting("sensitive");
    }
}

internal sealed record PokeDrawRequest(int? Index);
