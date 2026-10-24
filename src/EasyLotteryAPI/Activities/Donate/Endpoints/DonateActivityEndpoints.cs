using EasyLotteryApplication.DonateActivities;
using EasyLotteryDomain.Models.Config;
using EasyLotteryApi.Security;
using MediatR;
using EasyLotteryDomain.Services;
using System.Globalization;
using Results = Microsoft.AspNetCore.Http.Results;

namespace EasyLotteryApi.Activities.Donate.Endpoints;

internal static class DonateActivityEndpoints
{
    public static void MapDonateActivityEndpoints(this WebApplication app)
    {
        app.MapGet("/api/donate-lottery-draw-records", async (
            HttpContext context,
            int? page,
            int? pageSize,
            int? activityId,
            string? outcome,
            string? search,
            DateTimeOffset? fromUtc,
            DateTimeOffset? beforeUtc,
            IEasyLotteryConfigStore configStore,
            ObsSessionAccess sessionAccess) =>
        {
            var denied = ObsSessionAccess.DeniedResult(sessionAccess.RequireAdmin(context.Request));
            if (denied is not null) return denied;

            var pageNumber = page ?? 1;
            var resultPageSize = pageSize ?? 20;
            var resultType = (outcome ?? "all").Trim().ToLowerInvariant();
            var searchText = search?.Trim() ?? "";
            if (pageNumber < 1 || resultPageSize is < 1 or > 100 || (activityId.HasValue && activityId.Value < 1)
                || (fromUtc.HasValue && beforeUtc.HasValue && fromUtc.Value > beforeUtc.Value) || searchText.Length > 120
                || resultType is not ("all" or "winning" or "miss"))
            {
                return Results.BadRequest(new { error = "查詢條件或分頁參數無效。" });
            }

            var document = await configStore.LoadAsync(context.RequestAborted);
            var activityNames = (document.DonateLotteryActivities ?? [])
                .GroupBy(activity => activity.Id)
                .ToDictionary(group => group.Key, group => group.First().Name);
            var filtered = (document.DonateLotteryDrawRecords ?? []).AsEnumerable();
            if (activityId.HasValue) filtered = filtered.Where(record => record.ActivityId == activityId.Value);
            if (fromUtc.HasValue) filtered = filtered.Where(record => record.DrawnAtUtc >= fromUtc.Value);
            if (beforeUtc.HasValue) filtered = filtered.Where(record => record.DrawnAtUtc < beforeUtc.Value);
            if (resultType == "winning") filtered = filtered.Where(record => record.IsWinning);
            if (resultType == "miss") filtered = filtered.Where(record => !record.IsWinning);
            if (searchText.Length > 0)
            {
                filtered = filtered.Where(record =>
                    record.ActivityId.ToString(CultureInfo.InvariantCulture).Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || GetActivityName(record.ActivityId, activityNames).Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || (record.DonorName ?? "").Contains(searchText, StringComparison.OrdinalIgnoreCase)
                    || (record.PrizeName ?? "").Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }

            var ordered = filtered.OrderByDescending(record => record.DrawnAtUtc).ThenByDescending(record => record.Id);
            var totalCount = ordered.Count();
            var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)resultPageSize);
            var offset = (long)(pageNumber - 1) * resultPageSize;
            var skip = (int)Math.Min(offset, totalCount);
            var items = ordered.Skip(skip).Take(resultPageSize)
                .Select(record => new DonateLotteryDrawHistoryItem(
                    record.Id,
                    record.ActivityId,
                    GetActivityName(record.ActivityId, activityNames),
                    record.DrawnAtUtc,
                    record.DonorName,
                    record.Amount,
                    record.DrawCount,
                    record.PrizeName,
                    record.IsWinning))
                .ToArray();

            return Results.Ok(new DonateLotteryDrawHistoryPage(items, pageNumber, resultPageSize, totalCount, totalPages));
        });

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
            var denied = ObsSessionAccess.DeniedResult(sessionAccess.RequireObs(context.Request, ObsResourceKind.Donate, publicId.ToString(), ObsSessionScope.Control));
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

    private static string GetActivityName(int activityId, IReadOnlyDictionary<int, string> activityNames) =>
        activityNames.TryGetValue(activityId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : $"已刪除活動 #{activityId}";
}

internal sealed record DonateObsTestRequest(string DonorName, decimal Amount, string Message, string PaymentMethod);
internal sealed record DonateObsTestResponse(bool Processed, string Reason, DonateLotteryDrawRecord[] Records);
internal sealed record DonateLotteryDrawHistoryItem(
    int Id,
    int ActivityId,
    string ActivityName,
    DateTimeOffset DrawnAtUtc,
    string DonorName,
    decimal Amount,
    int DrawCount,
    string PrizeName,
    bool IsWinning);
internal sealed record DonateLotteryDrawHistoryPage(
    IReadOnlyList<DonateLotteryDrawHistoryItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
