using System.Text;
using System.Net;
using System.Net.Mail;
using System.Text.Json;
using EasyLotteryApplication.DonateActivities;
using EasyLotteryApplication.Payments;
using EasyLotteryInfrastructure;
using EasyLotteryDomain.Models.Overtime;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using EasyLotteryApi;
using EasyLotteryApi.Payments;
using EasyLotteryDomain.Models.Config;
using EasyLotteryDomain.Services;

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

Func<HttpContext, IEasyLotteryConfigRepository, AdminAccess, Task<IResult>> readSettings = async (context, settingsStore, adminAccess) =>
{
    if (!adminAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
    return Results.Text(await settingsStore.ReadForBrowserAsync(context.RequestAborted), "text/yaml", Encoding.UTF8);
};
app.MapGet("/settings", readSettings);
// Compatibility endpoint for browsers still serving a cached pre-settings.yaml build.
app.MapGet("/easy-lottery-config.yaml", readSettings);

Func<HttpContext, IHubContext<OvertimeHub>, IEasyLotteryConfigRepository, AdminAccess, Task<IResult>> writeSettings = async (context, hub, settingsStore, adminAccess) =>
{
    if (!adminAccess.IsAuthorized(context.Request)) return Results.Unauthorized();

    var content = await ReadRequestBodyAsync(context.Request, context.RequestAborted);
    await settingsStore.SaveBrowserUpdateAsync(content, context.RequestAborted);
    await hub.Clients.All.SendAsync("OvertimeStateChanged", context.RequestAborted);
    return Results.NoContent();
};
app.MapPut("/settings", writeSettings);
app.MapPut("/easy-lottery-config.yaml", writeSettings);

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

Func<HttpContext, IOvertimeFeedRepository, AdminAccess, Task<IResult>> readOvertimeFeed = async (context, feedRepository, adminAccess) =>
{
    if (!adminAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
    return Results.Text(JsonSerializer.Serialize(await feedRepository.ListAsync(context.RequestAborted)), "application/json", Encoding.UTF8);
};
app.MapGet("/api/overtime-feed", readOvertimeFeed);
app.MapGet("/easy-lottery-overtime-feed.json", readOvertimeFeed);

Func<HttpContext, IOvertimeFeedRepository, IHubContext<OvertimeHub>, AdminAccess, Task<IResult>> writeOvertimeFeed = async (context, feedRepository, hub, adminAccess) =>
{
    if (!adminAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
    var body = await ReadRequestBodyAsync(context.Request, context.RequestAborted);
    var events = string.IsNullOrWhiteSpace(body)
        ? []
        : JsonSerializer.Deserialize<List<OvertimeSupportEvent>>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
    await feedRepository.SaveAsync(events, context.RequestAborted);
    await hub.Clients.All.SendAsync("OvertimeFeedChanged", context.RequestAborted);
    return Results.NoContent();
};
app.MapPut("/api/overtime-feed", writeOvertimeFeed);
app.MapPut("/easy-lottery-overtime-feed.json", writeOvertimeFeed);

Func<HttpContext, IOvertimeFeedRepository, IHubContext<OvertimeHub>, AdminAccess, Task<IResult>> clearOvertimeFeed = async (context, feedRepository, hub, adminAccess) =>
{
    if (!adminAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
    await feedRepository.SaveAsync([], context.RequestAborted);
    await hub.Clients.All.SendAsync("OvertimeFeedChanged", context.RequestAborted);
    return Results.NoContent();
};
app.MapDelete("/api/overtime-feed", clearOvertimeFeed);
app.MapDelete("/easy-lottery-overtime-feed.json", clearOvertimeFeed);

app.MapPost("/api/result-notification", async (ResultNotificationRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Recipient) ||
        string.IsNullOrWhiteSpace(request.Smtp.Host) ||
        request.Smtp.Port <= 0 ||
        string.IsNullOrWhiteSpace(request.Smtp.FromAddress))
    {
        return Results.NoContent();
    }

    using var message = new MailMessage(
        new MailAddress(request.Smtp.FromAddress, request.Smtp.FromName),
        new MailAddress(request.Recipient))
    {
        Subject = request.Subject,
        Body = request.Body
    };
    using var client = new SmtpClient(request.Smtp.Host, request.Smtp.Port)
    {
        EnableSsl = request.Smtp.EnableSsl,
        Credentials = new NetworkCredential(request.Smtp.Username, request.Smtp.Password)
    };

    await client.SendMailAsync(message);
    return Results.NoContent();
});

app.MapHub<OvertimeHub>("/hubs/overtime");

app.MapGet("/api/tunnel", (TunnelRuntimeService tunnelRuntime) => Results.Ok(tunnelRuntime.GetStatus()));
app.MapPost("/api/tunnel/start", async (TunnelStartRequest request, TunnelRuntimeService tunnelRuntime, CancellationToken cancellationToken) =>
    Results.Ok(await tunnelRuntime.StartAsync(request.Provider, cancellationToken)));
app.MapDelete("/api/tunnel", async (TunnelRuntimeService tunnelRuntime, CancellationToken cancellationToken) =>
{
    await tunnelRuntime.StopAsync(cancellationToken);
    return Results.NoContent();
});

app.MapPost("/api/payments/{providerId}/notify", async (string providerId, HttpContext context, PaymentCallbackProcessor callbacks) =>
{
    var body = await ReadRequestBodyAsync(context.Request, context.RequestAborted);
    var headers = context.Request.Headers.ToDictionary(header => header.Key, header => header.Value.ToString(), StringComparer.OrdinalIgnoreCase);
    var result = await callbacks.ProcessAsync(providerId, new PaymentNotificationRequest
    {
        ContentType = context.Request.ContentType ?? "",
        Body = body,
        Headers = headers
    }, context.RequestAborted);

    if (!result.Accepted)
    {
        return Results.BadRequest();
    }

    // ECPay broadcaster requires this exact acknowledgement after the callback is received.
    // NewebPay only requires a successful HTTP response for its Form POST notification.
    var acknowledgement = providerId.Trim().Equals("ecpay", StringComparison.OrdinalIgnoreCase)
        ? "1|OK"
        : "OK";
    return Results.Text(acknowledgement, "text/plain", Encoding.UTF8);
});

app.MapPost("/api/payments/orders", async (PaymentOrderRegistrationRequest request, HttpContext context, PaymentCallbackProcessor callbacks, AdminAccess adminAccess) =>
{
    if (!adminAccess.IsAuthorized(context.Request)) return Results.Unauthorized();
    try { return Results.Created($"/api/payments/orders/{request.MerchantOrderNo}", await callbacks.RegisterOrderAsync(request, context.RequestAborted)); }
    catch (ArgumentException exception)
    {
        app.Logger.LogWarning(exception, "Invalid payment order registration request for merchant order {MerchantOrderNo}.", request.MerchantOrderNo);
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (InvalidOperationException exception)
    {
        app.Logger.LogWarning(exception, "Payment order registration conflict for merchant order {MerchantOrderNo}.", request.MerchantOrderNo);
        return Results.Conflict(new { error = exception.Message });
    }
});

app.MapGet("/api/payments/events", async (HttpContext context, PaymentCallbackProcessor callbacks, AdminAccess adminAccess) =>
    !adminAccess.IsAuthorized(context.Request) ? Results.Unauthorized() : Results.Ok(await callbacks.ListProcessedEventsAsync(context.RequestAborted)));

app.MapFallbackToFile("index.html");

await app.RunAsync();

static async Task<string> ReadRequestBodyAsync(HttpRequest request, CancellationToken cancellationToken)
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    return await reader.ReadToEndAsync(cancellationToken);
}

sealed class ResultNotificationRequest
{
    public string Recipient { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public SmtpSettings Smtp { get; set; } = new();
}

sealed class SmtpSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "";
    public bool EnableSsl { get; set; }
}
