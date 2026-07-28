using System.Text;
using EasyLotteryApplication.DonateActivities;
using EasyLotteryApplication.Payments;
using EasyLotteryInfrastructure;
using MediatR;
using EasyLotteryApi;
using EasyLotteryApi.Payments;
using EasyLotteryDomain.Services;
using EasyLotteryApi.Endpoints;

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
builder.Services.AddSingleton<PaymentCallbackProcessor>();

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

app.MapSettingsEndpoints();
app.MapObsSessionEndpoints();
app.MapOvertimeFeedEndpoints();
app.MapPaymentEndpoints();
app.MapDonateActivityEndpoints();
app.MapResultNotificationEndpoints();
app.MapTunnelEndpoints();
app.MapHub<OvertimeHub>("/hubs/overtime");

app.MapFallbackToFile("index.html");

await app.RunAsync();
