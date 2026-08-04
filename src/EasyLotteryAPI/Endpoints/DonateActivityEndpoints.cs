using EasyLotteryApplication.DonateActivities;
using EasyLotteryDomain.Models.Config;
using MediatR;
using EasyLotteryDomain.Services;

namespace EasyLotteryApi.Endpoints;

internal static class DonateActivityEndpoints
{
    public static void MapDonateActivityEndpoints(this WebApplication app)
    {
        Func<HttpContext, IMediator, ObsSessionAccess, Task<IResult>> listDonateActivities = async (context, mediator, sessionAccess) =>
        {
            var denied = ObsSessionAccess.DeniedResult(sessionAccess.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            return Results.Ok(await mediator.Send(new GetDonateActivitiesQuery(), context.RequestAborted));
        };
        app.MapGet("/api/donate-activities", listDonateActivities);

        Func<int, HttpContext, IMediator, ObsSessionAccess, Task<IResult>> getDonateActivityById = async (id, context, mediator, sessionAccess) =>
        {
            var denied = ObsSessionAccess.DeniedResult(sessionAccess.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            var activity = await mediator.Send(new GetDonateActivityByIdQuery(id), context.RequestAborted);
            return activity is null ? Results.NotFound(new { error = "找不到 Donate 活動。" }) : Results.Ok(activity);
        };
        app.MapGet("/api/donate-activities/{id:int}", getDonateActivityById);

        Func<DonateLotteryActivity, HttpContext, IMediator, ObsSessionAccess, Task<IResult>> createDonateActivity = async (activity, context, mediator, sessionAccess) =>
        {
            var denied = ObsSessionAccess.DeniedResult(sessionAccess.RequireAdmin(context.Request));
            if (denied is not null) return denied;
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
        app.MapPost("/api/donate-activities", createDonateActivity).RequireRateLimiting("sensitive");

        Func<int, DonateLotteryActivity, HttpContext, IMediator, ObsSessionAccess, Task<IResult>> updateDonateActivity = async (id, activity, context, mediator, sessionAccess) =>
        {
            var denied = ObsSessionAccess.DeniedResult(sessionAccess.RequireAdmin(context.Request));
            if (denied is not null) return denied;
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
        app.MapPut("/api/donate-activities/{id:int}", updateDonateActivity).RequireRateLimiting("sensitive");

        Func<int, HttpContext, IMediator, ObsSessionAccess, Task<IResult>> deleteDonateActivity = async (id, context, mediator, sessionAccess) =>
        {
            var denied = ObsSessionAccess.DeniedResult(sessionAccess.RequireAdmin(context.Request));
            if (denied is not null) return denied;
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
        app.MapDelete("/api/donate-activities/{id:int}", deleteDonateActivity).RequireRateLimiting("sensitive");

        app.MapPost("/api/obs/donate/{publicId:guid}/test", async (
            Guid publicId,
            DonateObsTestRequest request,
            HttpContext context,
            IEasyLotteryConfigStore configStore,
            ObsSessionAccess sessionAccess) =>
        {
            var denied = ObsSessionAccess.DeniedResult(sessionAccess.RequireObs(context.Request, "donate", publicId.ToString(), "control"));
            if (denied is not null) return denied;

            var document = await configStore.LoadAsync(context.RequestAborted);
            var activity = document.DonateLotteryActivities.FirstOrDefault(item => item.PublicId == publicId);
            if (activity is null) return Results.NotFound(new { error = "找不到 Donate 活動。" });
            var before = document.DonateLotteryDrawRecords.Count;
            var paymentId = $"obs-test:{publicId:N}:{Guid.NewGuid():N}";
            var processed = DonateLotteryEngine.Process(
                document,
                paymentId,
                request.DonorName,
                request.Amount,
                DateTimeOffset.UtcNow,
                donationMessage: request.Message,
                paymentMethod: request.PaymentMethod,
                targetActivityId: activity.Id,
                bypassActivityEligibility: true);
            await configStore.SaveAsync(document, context.RequestAborted);
            var records = document.DonateLotteryDrawRecords.Skip(before).ToArray();
            return Results.Ok(new DonateObsTestResponse(processed.Processed, processed.Reason, records));
        }).RequireRateLimiting("sensitive");
    }
}

internal sealed record DonateObsTestRequest(string DonorName, decimal Amount, string Message, string PaymentMethod);
internal sealed record DonateObsTestResponse(bool Processed, string Reason, DonateLotteryDrawRecord[] Records);
