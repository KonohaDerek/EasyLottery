using System.Text;
using EasyLotteryApplication.Settings;
using EasyLotteryApi;
using EasyLotteryDomain.Services;
using EasyLotteryInfrastructure.Settings;
using Microsoft.AspNetCore.SignalR;

namespace EasyLotteryApi.Endpoints;

internal static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this WebApplication app)
    {
        Func<HttpContext, IEasyLotteryConfigRepository, AdminAccess, Task<IResult>> readSettings = async (context, settingsStore, adminAccess) =>
        {
            if (!adminAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
            return Results.Text(await settingsStore.ReadForBrowserAsync(context.RequestAborted), "text/yaml", Encoding.UTF8);
        };
        app.MapGet("/settings", readSettings);
        app.MapGet("/easy-lottery-config.yaml", readSettings);

        Func<HttpContext, IHubContext<OvertimeHub>, IEasyLotteryConfigRepository, AdminAccess, Task<IResult>> writeSettings = async (context, hub, settingsStore, adminAccess) =>
        {
            if (!adminAccess.IsAuthorized(context.Request)) return Results.Unauthorized();

            var content = await HttpRequestBodyReader.ReadTextAsync(context.Request, context.RequestAborted);
            await settingsStore.SaveBrowserUpdateAsync(content, context.RequestAborted);
            await hub.Clients.All.SendAsync("OvertimeStateChanged", context.RequestAborted);
            return Results.NoContent();
        };
        app.MapPut("/settings", writeSettings);
        app.MapPut("/easy-lottery-config.yaml", writeSettings);
    }
}
