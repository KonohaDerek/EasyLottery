using EasyLotteryApplication.DonateActivities;
using EasyLotteryDomain.Models.Config;
using MediatR;

namespace EasyLotteryApi.Endpoints;

internal static class DonateActivityEndpoints
{
    public static void MapDonateActivityEndpoints(this WebApplication app)
    {
        Func<HttpContext, IMediator, ObsSessionAccess, Task<IResult>> listDonateActivities = async (context, mediator, sessionAccess) =>
        {
            if (!sessionAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
            return Results.Ok(await mediator.Send(new GetDonateActivitiesQuery(), context.RequestAborted));
        };
        app.MapGet("/api/donate-activities", listDonateActivities);

        Func<int, HttpContext, IMediator, ObsSessionAccess, Task<IResult>> getDonateActivityById = async (id, context, mediator, sessionAccess) =>
        {
            if (!sessionAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
            var activity = await mediator.Send(new GetDonateActivityByIdQuery(id), context.RequestAborted);
            return activity is null ? Results.NotFound(new { error = "找不到 Donate 活動。" }) : Results.Ok(activity);
        };
        app.MapGet("/api/donate-activities/{id:int}", getDonateActivityById);

        Func<DonateLotteryActivity, HttpContext, IMediator, ObsSessionAccess, Task<IResult>> createDonateActivity = async (activity, context, mediator, sessionAccess) =>
        {
            if (!sessionAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
            try
            {
                var saved = await mediator.Send(new SaveDonateActivityCommand(activity), context.RequestAborted);
                return Results.Created($"/api/donate-activities/{saved.Id}", saved);
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        };
        app.MapPost("/api/donate-activities", createDonateActivity);

        Func<int, DonateLotteryActivity, HttpContext, IMediator, ObsSessionAccess, Task<IResult>> updateDonateActivity = async (id, activity, context, mediator, sessionAccess) =>
        {
            if (!sessionAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
            if (activity.Id != 0 && activity.Id != id)
            {
                return Results.BadRequest(new { error = "路由 ID 與活動 ID 不一致。" });
            }

            try
            {
                activity.Id = id;
                var saved = await mediator.Send(new SaveDonateActivityCommand(activity), context.RequestAborted);
                return Results.Ok(saved);
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        };
        app.MapPut("/api/donate-activities/{id:int}", updateDonateActivity);

        Func<int, HttpContext, IMediator, ObsSessionAccess, Task<IResult>> deleteDonateActivity = async (id, context, mediator, sessionAccess) =>
        {
            if (!sessionAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
            try
            {
                await mediator.Send(new DeleteDonateActivityCommand(id), context.RequestAborted);
                return Results.NoContent();
            }
            catch (InvalidOperationException exception)
            {
                return Results.NotFound(new { error = exception.Message });
            }
        };
        app.MapDelete("/api/donate-activities/{id:int}", deleteDonateActivity);
    }
}
