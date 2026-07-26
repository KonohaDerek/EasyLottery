using System.Text;
using EasyLotteryApplication.DonateActivities;
using EasyLotteryApplication.Payments;
using EasyLotteryApplication.Settings;
using EasyLotteryInfrastructure;
using EasyLotteryInfrastructure.Settings;
using MediatR;
using EasyLotteryApi;
using EasyLotteryApi.Payments;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;
using EasyLotteryApi.Endpoints;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddSingleton<TunnelRuntimeService>();
builder.Services.AddSingleton<IPaymentProvider, EcpayBroadcasterPaymentProvider>();
builder.Services.AddSingleton<IPaymentProvider, NewebPayDonationPaymentProvider>();
builder.Services.AddSingleton<PaymentProviderFactory>();
builder.Services.AddSingleton<ConfigSecretRedactor>();
builder.Services.AddEasyLotteryInfrastructure();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(GetDonateActivitiesQuery).Assembly, typeof(DependencyInjection).Assembly));
builder.Services.AddSingleton<SettingsFileStore>();
builder.Services.AddSingleton<IEasyLotteryConfigRepository>(sp => sp.GetRequiredService<SettingsFileStore>());
builder.Services.AddSingleton<IEasyLotteryConfigStore>(sp => sp.GetRequiredService<SettingsFileStore>());
builder.Services.AddSingleton<AdminAccess>();
builder.Services.AddSingleton<PaymentCallbackProcessor>();

var storageDirectory = builder.Configuration["Storage:Directory"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(storageDirectory);

var app = builder.Build();

if (!app.Services.GetRequiredService<AdminAccess>().IsConfigured)
{
    app.Logger.LogWarning("Settings administration is disabled because Settings:AdminToken is not configured.");
}

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

Func<HttpContext, IMediator, AdminAccess, Task<IResult>> listDonateActivities = async (context, mediator, adminAccess) =>
{
    if (!adminAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
    return Results.Ok(await mediator.Send(new GetDonateActivitiesQuery(), context.RequestAborted));
};
app.MapGet("/api/donate-activities", listDonateActivities);

Func<int, HttpContext, IMediator, AdminAccess, Task<IResult>> getDonateActivityById = async (id, context, mediator, adminAccess) =>
{
    if (!adminAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
    var activity = await mediator.Send(new GetDonateActivityByIdQuery(id), context.RequestAborted);
    return activity is null ? Results.NotFound(new { error = "找不到 Donate 活動。" }) : Results.Ok(activity);
};
app.MapGet("/api/donate-activities/{id:int}", getDonateActivityById);

Func<DonateLotteryActivity, HttpContext, IMediator, AdminAccess, Task<IResult>> createDonateActivity = async (activity, context, mediator, adminAccess) =>
{
    if (!adminAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
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
app.MapPost("/api/donate-activities", createDonateActivity);

Func<int, DonateLotteryActivity, HttpContext, IMediator, AdminAccess, Task<IResult>> updateDonateActivity = async (id, activity, context, mediator, adminAccess) =>
{
    if (!adminAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
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
app.MapPut("/api/donate-activities/{id:int}", updateDonateActivity);

Func<int, HttpContext, IMediator, AdminAccess, Task<IResult>> deleteDonateActivity = async (id, context, mediator, adminAccess) =>
{
    if (!adminAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
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
app.MapDelete("/api/donate-activities/{id:int}", deleteDonateActivity);

app.MapSettingsEndpoints();
app.MapOvertimeFeedEndpoints();
app.MapPaymentEndpoints();
app.MapResultNotificationEndpoints();
app.MapTunnelEndpoints();
app.MapHub<OvertimeHub>("/hubs/overtime");

app.MapFallbackToFile("index.html");

await app.RunAsync();
