using System.Text;
using System.Text.Json;
using EasyLotteryApplication.Payments;
using EasyLotteryApi.Common;
using EasyLotteryApi.Obs;
using EasyLotteryApi.Security;
using EasyLotteryDomain.Models.Overtime;
using Microsoft.AspNetCore.SignalR;

namespace EasyLotteryApi.Obs.Endpoints;

internal static class OvertimeFeedEndpoints
{
    private const int MaxFeedEvents = 30;

    public static void MapOvertimeFeedEndpoints(this WebApplication app)
    {
        Func<HttpContext, IOvertimeFeedRepository, ObsSessionAccess, Task<IResult>> readOvertimeFeed = async (context, feedRepository, sessionAccess) =>
        {
            var denied = ObsSessionAccess.DeniedResult(sessionAccess.RequireObs(context.Request, ObsResourceKind.Overtime, "default", ObsSessionScope.Read));
            if (denied is not null) return denied;
            return Results.Text(JsonSerializer.Serialize(await feedRepository.ListAsync(context.RequestAborted)), "application/json", Encoding.UTF8);
        };
        app.MapGet("/api/overtime-feed", readOvertimeFeed);

        Func<HttpContext, IOvertimeFeedMutationRepository, IHubContext<OvertimeHub>, ObsSessionAccess, Task<IResult>> writeOvertimeFeed = async (context, feedRepository, hub, sessionAccess) =>
        {
            var denied = ObsSessionAccess.DeniedResult(sessionAccess.RequireObs(context.Request, ObsResourceKind.Overtime, "default", ObsSessionScope.Control));
            if (denied is not null) return denied;
            var body = await HttpRequestBodyReader.ReadTextAsync(context.Request, context.RequestAborted);
            var events = string.IsNullOrWhiteSpace(body)
                ? []
                : JsonSerializer.Deserialize<List<OvertimeSupportEvent>>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
            await feedRepository.MutateAsync(current =>
            {
                var protectedSuperChats = current
                    .Where(IsSuperChat)
                    .Select(EventKey)
                    .ToHashSet(StringComparer.Ordinal);
                var currentKeys = current.Select(EventKey).ToHashSet(StringComparer.Ordinal);
                var incomingKeys = events.Select(EventKey).ToHashSet(StringComparer.Ordinal);
                var incomingAdditions = incomingKeys.Where(key => !currentKeys.Contains(key)).ToHashSet(StringComparer.Ordinal);
                foreach (var item in current.Where(IsSuperChat))
                {
                    if (incomingKeys.Add(EventKey(item))) events.Add(item);
                }

                TrimFeed(events, protectedSuperChats, preferredKeys: incomingAdditions);
                current.Clear();
                current.AddRange(events);
                return Task.CompletedTask;
            }, context.RequestAborted);
            await hub.Clients.Group(OvertimeHub.GroupName).SendAsync("OvertimeFeedChanged", context.RequestAborted);
            return Results.NoContent();
        };
        app.MapPut("/api/overtime-feed", writeOvertimeFeed).RequireRateLimiting("sensitive");

        app.MapPost("/api/overtime-feed/superchat", async (
            OvertimeSupportEvent item,
            HttpContext context,
            IOvertimeFeedMutationRepository feedRepository,
            IHubContext<OvertimeHub> hub,
            ObsSessionAccess sessionAccess) =>
        {
            var denied = ObsSessionAccess.DeniedResult(sessionAccess.RequireObs(context.Request, ObsResourceKind.Overtime, "default", ObsSessionScope.Control));
            if (denied is not null) return denied;
            if (item.Source != OvertimeSupportSource.SuperChat
                || string.IsNullOrWhiteSpace(item.ExternalId)
                || item.ExternalId.Length > 256)
                return Results.BadRequest(new { error = "SuperChat 事件資料無效。" });

            var appended = false;
            await feedRepository.MutateAsync(events =>
            {
                if (events.Any(existing => existing.Source == OvertimeSupportSource.SuperChat
                    && string.Equals(existing.ExternalId, item.ExternalId, StringComparison.Ordinal)))
                    return Task.CompletedTask;

                item.SourceLabel = "YouTube SuperChat";
                events.Add(item);
                TrimFeed(events, preferred: item);
                appended = events.Any(existing => ReferenceEquals(existing, item));
                return Task.CompletedTask;
            }, context.RequestAborted);

            if (appended)
                await hub.Clients.Group(OvertimeHub.GroupName).SendAsync("OvertimeFeedChanged", context.RequestAborted);
            return Results.NoContent();
        }).RequireRateLimiting("sensitive");

        Func<HttpContext, IOvertimeFeedRepository, IHubContext<OvertimeHub>, ObsSessionAccess, Task<IResult>> clearOvertimeFeed = async (context, feedRepository, hub, sessionAccess) =>
        {
            var denied = ObsSessionAccess.DeniedResult(sessionAccess.RequireObs(context.Request, ObsResourceKind.Overtime, "default", ObsSessionScope.Control));
            if (denied is not null) return denied;
            await feedRepository.SaveAsync([], context.RequestAborted);
            await hub.Clients.Group(OvertimeHub.GroupName).SendAsync("OvertimeFeedChanged", context.RequestAborted);
            return Results.NoContent();
        };
        app.MapDelete("/api/overtime-feed", clearOvertimeFeed).RequireRateLimiting("sensitive");
    }

    private static bool IsSuperChat(OvertimeSupportEvent item) => item.Source == OvertimeSupportSource.SuperChat;

    private static string EventKey(OvertimeSupportEvent item) =>
        string.IsNullOrWhiteSpace(item.ExternalId) ? $"id:{item.Id:D}" : $"external:{item.ExternalId}";

    private static void TrimFeed(
        List<OvertimeSupportEvent> events,
        IReadOnlySet<string>? protectedKeys = null,
        OvertimeSupportEvent? preferred = null,
        IReadOnlySet<string>? preferredKeys = null)
    {
        while (events.Count > MaxFeedEvents)
        {
            var removable = events
                .Where(item => !IsPreferred(item) && (protectedKeys is null || !protectedKeys.Contains(EventKey(item))))
                .MinBy(item => item.OccurredAtUtc);
            removable ??= events.Where(item => !IsPreferred(item)).MinBy(item => item.OccurredAtUtc);
            removable ??= events.MinBy(item => item.OccurredAtUtc);
            if (removable is null) break;
            events.Remove(removable);
        }

        events.Sort((left, right) => right.OccurredAtUtc.CompareTo(left.OccurredAtUtc));

        bool IsPreferred(OvertimeSupportEvent item) => ReferenceEquals(item, preferred)
            || (preferredKeys?.Contains(EventKey(item)) ?? false);
    }
}
