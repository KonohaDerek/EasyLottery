using System.Text;
using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.SignalR;
using EasyLotteryApi;
using EasyLotteryApi.Payments;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddSingleton<TunnelRuntimeService>();
builder.Services.AddSingleton<IPaymentProvider, EcpayBroadcasterPaymentProvider>();
builder.Services.AddSingleton<IPaymentProvider, NewebPayDonationPaymentProvider>();
builder.Services.AddSingleton<PaymentProviderFactory>();
builder.Services.AddSingleton<PaymentCallbackProcessor>();

var storageDirectory = builder.Configuration["Storage:Directory"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(storageDirectory);

var configPath = Path.Combine(storageDirectory, "settings.yaml");
var legacyConfigPath = Path.Combine(storageDirectory, "easy-lottery.yaml");
var overtimeFeedPath = Path.Combine(storageDirectory, "easy-lottery-overtime-feed.json");
var storageGate = new SemaphoreSlim(1, 1);
var configSecrets = new ConfigSecretRedactor();

// Preserve existing installations while making settings.yaml the single
// canonical configuration file from now on.
if (!File.Exists(configPath) && File.Exists(legacyConfigPath))
{
    File.Move(legacyConfigPath, configPath);
}

if (File.Exists(configPath))
{
    var currentSettings = await File.ReadAllTextAsync(configPath);
    var normalizedSettings = configSecrets.NormalizeForPersistence(currentSettings);
    if (!string.Equals(currentSettings, normalizedSettings, StringComparison.Ordinal))
    {
        await WriteFileAsync(configPath, normalizedSettings, storageGate, CancellationToken.None);
    }
}

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

Func<HttpContext, Task<IResult>> readSettings = async context =>
{
    var content = await ReadFileAsync(configPath, string.Empty, context.RequestAborted);
    return Results.Text(configSecrets.RedactForBrowser(content), "text/yaml", Encoding.UTF8);
};
app.MapGet("/settings", readSettings);
// Compatibility endpoint for browsers still serving a cached pre-settings.yaml build.
app.MapGet("/easy-lottery-config.yaml", readSettings);

Func<HttpContext, IHubContext<OvertimeHub>, Task<IResult>> writeSettings = async (context, hub) =>
{
    // Public callback tunnels must never also provide a public settings write API.
    // Remote administration can be explicitly enabled only by the host operator.
    var allowRemoteSettingsWrite = builder.Configuration.GetValue<bool>("Settings:AllowRemoteWrite");
    if (!allowRemoteSettingsWrite && (context.Connection.RemoteIpAddress is null || !IPAddress.IsLoopback(context.Connection.RemoteIpAddress)))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var content = await ReadRequestBodyAsync(context.Request, context.RequestAborted);
    var existingContent = await ReadFileAsync(configPath, string.Empty, context.RequestAborted);
    var mergedContent = configSecrets.MergeBrowserUpdate(existingContent, content);
    await WriteFileAsync(configPath, mergedContent, storageGate, context.RequestAborted);
    await hub.Clients.All.SendAsync("OvertimeStateChanged", context.RequestAborted);
    return Results.NoContent();
};
app.MapPut("/settings", writeSettings);
app.MapPut("/easy-lottery-config.yaml", writeSettings);

app.MapGet("/easy-lottery-overtime-feed.json", async (HttpContext context) =>
{
    var content = await ReadFileAsync(overtimeFeedPath, "[]", context.RequestAborted);
    return Results.Text(content, "application/json", Encoding.UTF8);
});

app.MapPut("/easy-lottery-overtime-feed.json", async (HttpContext context, IHubContext<OvertimeHub> hub) =>
{
    var content = await ReadRequestBodyAsync(context.Request, context.RequestAborted);
    await WriteFileAsync(overtimeFeedPath, content, storageGate, context.RequestAborted);
    await hub.Clients.All.SendAsync("OvertimeFeedChanged", context.RequestAborted);
    return Results.NoContent();
});

app.MapDelete("/easy-lottery-overtime-feed.json", async (HttpContext context, IHubContext<OvertimeHub> hub) =>
{
    await WriteFileAsync(overtimeFeedPath, "[]", storageGate, context.RequestAborted);
    await hub.Clients.All.SendAsync("OvertimeFeedChanged", context.RequestAborted);
    return Results.NoContent();
});

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

app.MapFallbackToFile("index.html");

await app.RunAsync();

static async Task<string> ReadFileAsync(string path, string fallback, CancellationToken cancellationToken)
{
    return File.Exists(path)
        ? await File.ReadAllTextAsync(path, cancellationToken)
        : fallback;
}

static async Task<string> ReadRequestBodyAsync(HttpRequest request, CancellationToken cancellationToken)
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    return await reader.ReadToEndAsync(cancellationToken);
}

static async Task WriteFileAsync(string path, string content, SemaphoreSlim gate, CancellationToken cancellationToken)
{
    await gate.WaitAsync(cancellationToken);
    try
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporaryPath, content, Encoding.UTF8, cancellationToken);
        File.Move(temporaryPath, path, overwrite: true);
    }
    finally
    {
        gate.Release();
    }
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
