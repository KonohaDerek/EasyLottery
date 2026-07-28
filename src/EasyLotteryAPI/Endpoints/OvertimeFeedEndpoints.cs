using System.Text;
using System.Text.Json;
using EasyLotteryApplication.Payments;
using EasyLotteryApi;
using EasyLotteryDomain.Models.Overtime;
using Microsoft.AspNetCore.SignalR;

namespace EasyLotteryApi.Endpoints;

internal static class OvertimeFeedEndpoints
{
    public static void MapOvertimeFeedEndpoints(this WebApplication app)
    {
        Func<HttpContext, IOvertimeFeedRepository, ObsSessionAccess, Task<IResult>> readOvertimeFeed = async (context, feedRepository, sessionAccess) =>
        {
            if (!sessionAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
            return Results.Text(JsonSerializer.Serialize(await feedRepository.ListAsync(context.RequestAborted)), "application/json", Encoding.UTF8);
        };
        app.MapGet("/api/overtime-feed", readOvertimeFeed);

        Func<HttpContext, IOvertimeFeedRepository, IHubContext<OvertimeHub>, ObsSessionAccess, Task<IResult>> writeOvertimeFeed = async (context, feedRepository, hub, sessionAccess) =>
        {
            if (!sessionAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
            var body = await HttpRequestBodyReader.ReadTextAsync(context.Request, context.RequestAborted);
            var events = string.IsNullOrWhiteSpace(body)
                ? []
                : JsonSerializer.Deserialize<List<OvertimeSupportEvent>>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
            await feedRepository.SaveAsync(events, context.RequestAborted);
            await hub.Clients.All.SendAsync("OvertimeFeedChanged", context.RequestAborted);
            return Results.NoContent();
        };
        app.MapPut("/api/overtime-feed", writeOvertimeFeed);

        Func<HttpContext, IOvertimeFeedRepository, IHubContext<OvertimeHub>, ObsSessionAccess, Task<IResult>> clearOvertimeFeed = async (context, feedRepository, hub, sessionAccess) =>
        {
            if (!sessionAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
            await feedRepository.SaveAsync([], context.RequestAborted);
            await hub.Clients.All.SendAsync("OvertimeFeedChanged", context.RequestAborted);
            return Results.NoContent();
        };
        app.MapDelete("/api/overtime-feed", clearOvertimeFeed);
    }
}
