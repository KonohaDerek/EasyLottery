using EasyLotteryApi.Security;
using EasyLotteryInfrastructure.Storage;
using Microsoft.Extensions.Options;

namespace EasyLotteryApi.Shop;

internal static class ShopEndpoints
{
    public static void MapShopEndpoints(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Shop:Enabled")) return;

        app.MapGet("/api/shop/status", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { enabled = true });
        });

        app.MapGet("/api/shop/hub-link", async (
            HttpContext context,
            ObsSessionAccess access,
            IOptions<StorageProviderOptions> storage,
            HubLinkSettingsService settings) =>
        {
            var denied = RequireAdmin(context, access);
            if (denied is not null) return denied;
            if (!IsSqlite(storage)) return StorageRequired();

            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(await settings.ReadAsync(context.RequestAborted));
        }).RequireRateLimiting("sensitive");

        app.MapPut("/api/shop/hub-link", async (
            HubLinkWriteRequest request,
            HttpContext context,
            ObsSessionAccess access,
            IOptions<StorageProviderOptions> storage,
            HubLinkSettingsService settings) =>
        {
            var denied = RequireAdmin(context, access);
            if (denied is not null) return denied;
            if (!IsSqlite(storage)) return StorageRequired();

            try
            {
                context.Response.Headers.CacheControl = "no-store";
                return Results.Ok(await settings.SaveAsync(request, context.RequestAborted));
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        }).RequireRateLimiting("sensitive");

        app.MapPost("/api/shop/hub-link/test", async (
            HubLinkWriteRequest request,
            HttpContext context,
            ObsSessionAccess access,
            IOptions<StorageProviderOptions> storage,
            HubLinkSettingsService settings) =>
        {
            var denied = RequireAdmin(context, access);
            if (denied is not null) return denied;
            if (!IsSqlite(storage)) return StorageRequired();

            try
            {
                context.Response.Headers.CacheControl = "no-store";
                return Results.Ok(await settings.TestAsync(request, context.RequestAborted));
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        }).RequireRateLimiting("sensitive");
    }

    private static IResult? RequireAdmin(HttpContext context, ObsSessionAccess access) =>
        ObsSessionAccess.DeniedResult(access.RequireAdmin(context.Request));

    private static bool IsSqlite(IOptions<StorageProviderOptions> storage) =>
        storage.Value.NormalizedProvider == "sqlite";

    private static IResult StorageRequired() => Results.Problem(
        statusCode: StatusCodes.Status503ServiceUnavailable,
        title: "卡牌商店需要 SQLite 儲存",
        detail: "請先將 Storage:Provider 設為 sqlite，再使用 Card Hub 連結設定。");
}
