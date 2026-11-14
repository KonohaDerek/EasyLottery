using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace EasyLotteryApi.CardShop;

internal static class ShopFeatureGate
{
    public const string FeatureStatusPath = "/api/shop/feature-status";

    public static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue("Shop:Enabled", false);

    public static bool ShouldBlock(string path, bool enabled) =>
        !enabled
        && new PathString(path).StartsWithSegments("/api/shop", StringComparison.OrdinalIgnoreCase)
        && !path.Equals(FeatureStatusPath, StringComparison.OrdinalIgnoreCase);

    public static IApplicationBuilder UseShopFeatureGate(this IApplicationBuilder app, IConfiguration configuration)
    {
        var enabled = IsEnabled(configuration);
        return app.Use(async (context, next) =>
        {
            if (ShouldBlock(context.Request.Path, enabled))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "卡牌商店尚未啟用", extensions: new Dictionary<string, object?> { ["code"] = "shop_disabled" })
                    .ExecuteAsync(context);
                return;
            }

            await next();
        });
    }

    public static void MapShopFeatureEndpoints(this WebApplication app)
    {
        app.MapGet(FeatureStatusPath, (IConfiguration configuration) =>
            Results.Ok(new { enabled = IsEnabled(configuration) }));
    }
}
