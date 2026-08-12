using System.Text;
using EasyLotteryApplication.DonateActivities;
using EasyLotteryApplication.Payments;
using EasyLotteryInfrastructure;
using MediatR;
using EasyLotteryApi.Activities.Donate;
using EasyLotteryApi.Activities.Donate.Endpoints;
using EasyLotteryApi.Activities.PokeBox.Endpoints;
using EasyLotteryApi.Activities.ActivityResults.Endpoints;
using EasyLotteryApi.Activities.Roulette.Endpoints;
using EasyLotteryApi.Passkey;
using EasyLotteryApi.Passkey.Endpoints;
using EasyLotteryApi.Obs;
using EasyLotteryApi.Obs.Endpoints;
using EasyLotteryApi.Payments;
using EasyLotteryApi.Payments.Endpoints;
using EasyLotteryApi.Security;
using EasyLotteryApi.Security.Endpoints;
using EasyLotteryApi.Settings;
using EasyLotteryApi.Settings.Endpoints;
using EasyLotteryApi.Storage;
using EasyLotteryApi.Storage.Endpoints;
using EasyLotteryApi.Tunnel;
using EasyLotteryApi.Tunnel.Endpoints;
using EasyLotteryDomain.Services;
using EasyLotteryDomain.Models.Obs;
using EasyLotteryInfrastructure.Settings;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddSingleton<TunnelRuntimeService>();
builder.Services.AddSingleton<IPaymentProvider, EcpayBroadcasterPaymentProvider>();
builder.Services.AddSingleton<IPaymentProvider, NewebPayDonationPaymentProvider>();
builder.Services.AddSingleton<PaymentProviderFactory>();
builder.Services.AddEasyLotteryInfrastructure();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(GetDonateActivitiesQuery).Assembly, typeof(DependencyInjection).Assembly));
var adminTokenOptions = AdminTokenSecurityOptions.Load(builder.Configuration);
var adminPasskeyOptions = AdminPasskeyOptions.Load(builder.Configuration);
builder.Services.AddSingleton(adminTokenOptions);
builder.Services.AddSingleton(adminPasskeyOptions);
builder.Services.AddSingleton<AdminTokenIssuancePolicy>();
builder.Services.AddSingleton<ObsSessionTokenService>();
builder.Services.AddSingleton<ObsSessionAccess>();
builder.Services.AddSingleton<PasskeyStateStore>();
builder.Services.AddSingleton<PasskeyAuthenticationService>();
builder.Services.AddSingleton<ObsSettingsProjectionService>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("authentication", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("sensitive", context => RateLimitPartition.GetSlidingWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new SlidingWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0 }));
});
builder.Services.AddSingleton<PaymentCallbackProcessor>();
builder.Services.AddSingleton<PokeService>();
builder.Services.AddSingleton<RouletteService>();
builder.Services.AddSingleton<LiveDrawSessionService>();
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks()
    .AddCheck<StorageHealthCheck>("storage", tags: ["ready"]);
builder.Services.AddScoped<AiCongratulationProvider>();

var storageDirectory = builder.Configuration["Storage:Directory"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(storageDirectory);

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
foreach (var trustedProxy in adminTokenOptions.TrustedProxyIps)
{
    forwardedHeadersOptions.KnownProxies.Add(trustedProxy);
}

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (statusCode, title, detail) = exception switch
    {
        ConfigurationConcurrencyException concurrency =>
            (StatusCodes.Status409Conflict, "設定版本衝突", concurrency.Message),
        YamlStorageException =>
            (StatusCodes.Status503ServiceUnavailable, "設定儲存資料無法使用", "設定檔格式損壞或正在復原，請檢查儲存診斷與備份。"),
        _ =>
            (StatusCodes.Status500InternalServerError, "伺服器發生未預期錯誤", "請稍後再試，並檢查伺服器記錄。")
    };

    context.Response.StatusCode = statusCode;
    await Results.Problem(statusCode: statusCode, title: title, detail: detail).ExecuteAsync(context);
}));

if (app.Environment.IsDevelopment())
{
    // Debug builds generate hash-named WASM assets. Prevent a browser that was
    // left open across a rebuild from mixing an old boot manifest with new files.
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/_framework"))
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.CacheControl = "no-store";
                return Task.CompletedTask;
            });
        }

        await next();
    });
}

app.UseBlazorFrameworkFiles();
if (adminTokenOptions.TrustedProxyIps.Count > 0)
{
    app.UseForwardedHeaders(forwardedHeadersOptions);
}
app.UseStaticFiles();
app.Use(async (context, next) =>
{
    // Normal API payloads stay small; media assets have their own repository limit
    // and need enough room for multipart boundaries.
    var configuredAssetBytes = ObsAssetLimits.ReadMaxAssetBytes(app.Configuration["Storage:MaxAssetBytes"]);
    var maxRequestBytes = context.Request.Path.StartsWithSegments("/api/obs-assets/import")
        ? ObsAssetLimits.MaximumPackageBytes + ObsAssetLimits.RequestOverheadBytes
        : context.Request.Path.StartsWithSegments("/api/obs-assets")
        ? configuredAssetBytes + ObsAssetLimits.RequestOverheadBytes
        : 1_048_576L;
    var bodySizeFeature = context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
    if (bodySizeFeature is { IsReadOnly: false }) bodySizeFeature.MaxRequestBodySize = maxRequestBytes;
    if (context.Request.ContentLength > maxRequestBytes)
    {
        app.Logger.LogWarning("Security request rejected: payload too large for {Method} {Path} from {RemoteIp}", context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress);
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        return;
    }
    await next();
    if (context.Response.StatusCode is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden or StatusCodes.Status429TooManyRequests)
    {
        app.Logger.LogWarning("Security request rejected: HTTP {StatusCode} for {Method} {Path} from {RemoteIp}", context.Response.StatusCode, context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress);
    }
});
app.UseRateLimiter();

app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapSettingsEndpoints();
app.MapSettingsResourceEndpoints();
app.MapPasskeyEndpoints();
app.MapObsSessionEndpoints();
app.MapAiCongratulationEndpoints();
app.MapOvertimeFeedEndpoints();
app.MapPaymentEndpoints();
app.MapDonateActivityEndpoints();
app.MapPokeTemplateEndpoints();
app.MapRouletteTemplateEndpoints();
app.MapActivityResultEndpoints();
app.MapObsAssetEndpoints();
app.MapStorageEndpoints();
app.MapResultNotificationEndpoints();
app.MapTunnelEndpoints();
app.MapLiveDrawSessionEndpoints();
app.MapHub<OvertimeHub>("/hubs/overtime");
app.MapHub<LiveDrawHub>("/hubs/live-draw");

app.MapGet("/api/test/session-token", (IHostEnvironment environment, IConfiguration configuration, ObsSessionTokenService tokens) =>
{
    if (!environment.IsDevelopment() || !configuration.GetValue<bool>("Testing:EnableAdminSessionToken"))
    {
        return Results.NotFound();
    }

    // Test-only bridge for browser E2E setup; never issues tokens in production.
    var issued = tokens.IssueAdminToken();
    return Results.Ok(new SessionTokenResponse(issued.Token, issued.ExpiresAtUtc));
});

app.MapFallbackToFile("index.html");

await app.RunAsync();

public partial class Program;
