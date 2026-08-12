using EasyLotteryDomain.Services;
using EasyLotteryInfrastructure.Settings;
using EasyLotteryInfrastructure.Storage;
using EasyLotteryApi.Security;
using Microsoft.Extensions.Options;

namespace EasyLotteryApi.Storage.Endpoints;

internal static class StorageEndpoints
{
    public static void MapStorageEndpoints(this WebApplication app)
    {
        app.MapPost("/api/storage/import-yaml", async (
            HttpContext context,
            IOptions<StorageProviderOptions> options,
            SettingsFileStore yamlStore,
            IEasyLotteryConfigStore targetStore,
            ObsSessionAccess access) =>
        {
            var denied = ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));
            if (denied is not null) return denied;
            if (options.Value.NormalizedProvider != "sqlite")
                return Results.Conflict(new { message = "只有 Storage:Provider=sqlite 時才能匯入 YAML。" });

            var document = await yamlStore.ReadAsync(context.RequestAborted);
            await targetStore.SaveAsync(document, context.RequestAborted);
            return Results.Ok(new
            {
                imported = true,
                donateActivities = document.DonateLotteryActivities.Count,
                pokeTemplates = document.PokeTemplates.Count,
                rouletteTemplates = document.RouletteTemplates.Count,
                activityResults = document.ActivityResults.Count
            });
        }).RequireRateLimiting("sensitive");
    }
}
