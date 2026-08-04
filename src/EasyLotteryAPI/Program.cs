using System.Text;
using EasyLotteryApplication.DonateActivities;
using EasyLotteryApplication.Payments;
using EasyLotteryInfrastructure;
using MediatR;
using EasyLotteryApi;
using EasyLotteryApi.Payments;
using EasyLotteryDomain.Services;
using EasyLotteryApi.Endpoints;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddSingleton<TunnelRuntimeService>();
builder.Services.AddSingleton<IPaymentProvider, EcpayBroadcasterPaymentProvider>();
builder.Services.AddSingleton<IPaymentProvider, NewebPayDonationPaymentProvider>();
builder.Services.AddSingleton<PaymentProviderFactory>();
builder.Services.AddEasyLotteryInfrastructure();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(GetDonateActivitiesQuery).Assembly, typeof(DependencyInjection).Assembly));
builder.Services.AddSingleton<ObsSessionTokenService>();
builder.Services.AddSingleton<ObsSessionAccess>();
builder.Services.AddSingleton<AdminCredentialService>();
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
builder.Services.AddScoped<AiCongratulationProvider>();

var storageDirectory = builder.Configuration["Storage:Directory"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(storageDirectory);

var app = builder.Build();

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
app.UseStaticFiles();
app.Use(async (context, next) =>
{
    const long maxRequestBytes = 1_048_576;
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

app.MapSettingsEndpoints();
app.MapObsSessionEndpoints();
app.MapAiCongratulationEndpoints();
app.MapOvertimeFeedEndpoints();
app.MapPaymentEndpoints();
app.MapDonateActivityEndpoints();
app.MapResultNotificationEndpoints();
app.MapTunnelEndpoints();
app.MapLiveDrawSessionEndpoints();
app.MapHub<OvertimeHub>("/hubs/overtime");
app.MapHub<LiveDrawHub>("/hubs/live-draw");

app.MapFallbackToFile("index.html");

await app.RunAsync();

public partial class Program;
